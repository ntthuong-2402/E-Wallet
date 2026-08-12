using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.PaymentLedger.Application.Actors;
using Friday.Modules.PaymentLedger.Application.Features;
using Friday.Modules.PaymentLedger.Application.Models;
using Friday.Modules.PaymentLedger.Application.Persistence;
using Friday.Modules.PaymentLedger.Infrastructure;
using Friday.Modules.PaymentLedger.Infrastructure.Persistence;
using Friday.Modules.PaymentLedger.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Friday.Modules.PaymentLedger.PostgreSqlTests;

public sealed class PaymentLedgerPostgreSqlTests
{
    [Fact]
    public async Task Clean_database_enforces_concurrent_debit_idempotency_and_reversal()
    {
        string adminConnection = Environment.GetEnvironmentVariable("FRIDAY_PAYMENT_LEDGER_TEST_PG")
            ?? "Host=127.0.0.1;Port=5432;Database=postgres;Username=friday;Password=friday;Timeout=15";
        string databaseName = $"friday_ledger_it_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(adminConnection, databaseName);
        string connectionString = new NpgsqlConnectionStringBuilder(adminConnection)
        {
            Database = databaseName,
        }.ConnectionString;

        try
        {
            await using (PaymentLedgerDbContext db = new(CreateOptions(connectionString)))
                await db.Database.MigrateAsync();

            Guid source = Guid.NewGuid();
            Guid destinationA = Guid.NewGuid();
            Guid destinationB = Guid.NewGuid();
            await SeedAccountAsync(connectionString, source, "SOURCE", 100m);
            await SeedAccountAsync(connectionString, destinationA, "DEST-A", 0m);
            await SeedAccountAsync(connectionString, destinationB, "DEST-B", 0m);

            Task<TransactionDto>[] competing =
            [
                ExecuteTransferAsync(connectionString, "actor-a", "debit-a", source, destinationA, 80m),
                ExecuteTransferAsync(connectionString, "actor-b", "debit-b", source, destinationB, 80m),
            ];
            List<TransactionDto> posted = [];
            List<FridayException> rejected = [];
            foreach (Task<TransactionDto> task in competing)
            {
                try { posted.Add(await task); }
                catch (FridayException ex) { rejected.Add(ex); }
            }

            Assert.Single(posted);
            Assert.Single(rejected);
            Assert.Equal("LEDGER_INSUFFICIENT_FUNDS", rejected[0].ErrorCode);
            Assert.Equal(20m, await ReadBalanceAsync(connectionString, source));
            Assert.Equal(80m, await ReadBalanceAsync(connectionString, destinationA)
                + await ReadBalanceAsync(connectionString, destinationB));

            Guid replaySource = Guid.NewGuid();
            Guid replayDestination = Guid.NewGuid();
            await SeedAccountAsync(connectionString, replaySource, "REPLAY-SOURCE", 100m);
            await SeedAccountAsync(connectionString, replayDestination, "REPLAY-DEST", 0m);
            Task<TransactionDto>[] duplicates =
            [
                ExecuteTransferAsync(connectionString, "same-actor", "same-ref", replaySource, replayDestination, 40m),
                ExecuteTransferAsync(connectionString, "same-actor", "same-ref", replaySource, replayDestination, 40m),
            ];
            TransactionDto[] replayed = await Task.WhenAll(duplicates);
            Assert.Equal(replayed[0].Id, replayed[1].Id);
            Assert.Equal(60m, await ReadBalanceAsync(connectionString, replaySource));

            TransactionDto reversal = await ExecuteReversalAsync(
                connectionString,
                replayed[0].Id,
                "reverse-ref"
            );
            Assert.Equal(replayed[0].Id, reversal.OriginalTransactionId);
            Assert.Equal(100m, await ReadBalanceAsync(connectionString, replaySource));
            Assert.Equal(0m, await ReadBalanceAsync(connectionString, replayDestination));

            await AssertAppendOnlyAsync(connectionString, replayed[0].JournalId);
            await AssertInvalidLedgerShapesRejectedAsync(
                connectionString,
                source,
                destinationA,
                posted[0].Id
            );
            await AssertRelationalShapeAsync(connectionString);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabaseAsync(adminConnection, databaseName);
        }
    }

    private static DbContextOptions<PaymentLedgerDbContext> CreateOptions(string connectionString)
    {
        DbContextOptionsBuilder<PaymentLedgerDbContext> builder = new();
        builder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(PaymentLedgerDbContext).Assembly.FullName);
            npgsql.MigrationsHistoryTable(
                PaymentLedgerDbContext.MigrationHistoryTableName,
                PaymentLedgerDbContext.SchemaName
            );
        });
        return builder.Options;
    }

    private static ServiceProvider CreateProvider(string connectionString, string actor)
    {
        Dictionary<string, string?> values = new()
        {
            ["ConnectionStrings:PaymentLedgerDb"] = connectionString,
        };
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build());
        services.AddSingleton<IPaymentLedgerActor>(new TestActor(actor));
        services.AddSingleton(TimeProvider.System);
        services.AddPaymentLedgerInfrastructure(
            services.BuildServiceProvider().GetRequiredService<IConfiguration>()
        );
        return services.BuildServiceProvider();
    }

    private static async Task<TransactionDto> ExecuteTransferAsync(
        string connectionString,
        string actor,
        string refId,
        Guid source,
        Guid destination,
        decimal amount
    )
    {
        await using ServiceProvider provider = CreateProvider(connectionString, actor);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IPaymentLedgerUnitOfWork unitOfWork = scope.ServiceProvider
            .GetRequiredService<IPaymentLedgerUnitOfWork>();
        CreateInternalTransferHandler handler = new(
            scope.ServiceProvider.GetRequiredService<Friday.Modules.PaymentLedger.Domain.Repositories.ILedgerAccountRepository>(),
            scope.ServiceProvider.GetRequiredService<Friday.Modules.PaymentLedger.Domain.Repositories.IFinancialTransactionRepository>(),
            scope.ServiceProvider.GetRequiredService<Friday.Modules.PaymentLedger.Domain.Repositories.IIdempotencyRepository>(),
            scope.ServiceProvider.GetRequiredService<IPaymentLedgerActor>(),
            TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<Friday.Modules.PaymentLedger.Domain.Repositories.IFinancialAuditRepository>()
        );
        await unitOfWork.BeginTransactionAsync();
        try
        {
            TransactionDto result = await handler.HandleAsync(
                new(refId, source, destination, amount, "VND", "postgres verification"),
                default
            );
            await unitOfWork.CommitAsync();
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
    }

    private static async Task<TransactionDto> ExecuteReversalAsync(
        string connectionString,
        Guid originalId,
        string refId
    )
    {
        await using ServiceProvider provider = CreateProvider(connectionString, "reversal-actor");
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IPaymentLedgerUnitOfWork unitOfWork = scope.ServiceProvider
            .GetRequiredService<IPaymentLedgerUnitOfWork>();
        ReverseInternalTransferHandler handler = new(
            scope.ServiceProvider.GetRequiredService<Friday.Modules.PaymentLedger.Domain.Repositories.ILedgerAccountRepository>(),
            scope.ServiceProvider.GetRequiredService<Friday.Modules.PaymentLedger.Domain.Repositories.IFinancialTransactionRepository>(),
            scope.ServiceProvider.GetRequiredService<Friday.Modules.PaymentLedger.Domain.Repositories.IIdempotencyRepository>(),
            scope.ServiceProvider.GetRequiredService<IPaymentLedgerActor>(),
            TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<Friday.Modules.PaymentLedger.Domain.Repositories.IFinancialAuditRepository>()
        );
        await unitOfWork.BeginTransactionAsync();
        try
        {
            TransactionDto result = await handler.HandleAsync(
                new(originalId, refId, "PostgreSQL reversal verification"),
                default
            );
            await unitOfWork.CommitAsync();
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
    }

    private static async Task SeedAccountAsync(
        string connectionString,
        Guid id,
        string accountRef,
        decimal balance
    )
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            INSERT INTO payment_ledger.ledger_accounts
                ("Id", "AccountRef", "Currency", "Status", "AvailableBalance",
                 "Version", "CreatedOnUtc", "UpdatedOnUtc")
            VALUES (@id, @ref, 'VND', 'Active', @balance, 0, now(), now());
            """,
            connection
        );
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("ref", accountRef);
        command.Parameters.AddWithValue("balance", balance);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<decimal> ReadBalanceAsync(string connectionString, Guid id)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT \"AvailableBalance\" FROM payment_ledger.ledger_accounts WHERE \"Id\"=@id",
            connection
        );
        command.Parameters.AddWithValue("id", id);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    private static async Task AssertAppendOnlyAsync(string connectionString, Guid journalId)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "UPDATE payment_ledger.journals SET \"PostedOnUtc\"=now() WHERE \"Id\"=@id",
            connection
        );
        command.Parameters.AddWithValue("id", journalId);
        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync()
        );
        Assert.Equal("55000", ex.SqlState);
    }

    private static async Task AssertRelationalShapeAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT
              (SELECT count(*) FROM payment_ledger."__EFMigrationsHistory"),
              (SELECT count(*) FROM payment_ledger.financial_transactions),
              (SELECT count(*) FROM payment_ledger.journals),
              (SELECT count(*) FROM payment_ledger.journal_entries),
              (SELECT count(*) FROM payment_ledger.financial_audit_records);
            """,
            connection
        );
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt64(0));
        Assert.Equal(reader.GetInt64(1), reader.GetInt64(2));
        Assert.Equal(reader.GetInt64(2) * 2, reader.GetInt64(3));
        Assert.Equal(3, reader.GetInt64(4));
    }

    private static async Task AssertInvalidLedgerShapesRejectedAsync(
        string connectionString,
        Guid source,
        Guid destination,
        Guid postedTransactionId
    )
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();

        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            Guid transactionId = Guid.NewGuid();
            Guid journalId = Guid.NewGuid();
            await using NpgsqlCommand emptyJournal = new(
                """
                INSERT INTO payment_ledger.financial_transactions
                    ("Id", "Type", "Status", "SourceAccountId", "DestinationAccountId",
                     "Amount", "Currency", "PostedOnUtc")
                VALUES (@transaction, 'InternalTransfer', 'Posted', @source, @destination,
                        10, 'VND', now());
                INSERT INTO payment_ledger.journals ("Id", "TransactionId", "PostedOnUtc")
                VALUES (@journal, @transaction, now());
                """,
                connection,
                transaction
            );
            emptyJournal.Parameters.AddWithValue("transaction", transactionId);
            emptyJournal.Parameters.AddWithValue("journal", journalId);
            emptyJournal.Parameters.AddWithValue("source", source);
            emptyJournal.Parameters.AddWithValue("destination", destination);
            await emptyJournal.ExecuteNonQueryAsync();
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(
                () => transaction.CommitAsync()
            );
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }

        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            Guid transactionId = Guid.NewGuid();
            Guid journalId = Guid.NewGuid();
            await using NpgsqlCommand mismatched = new(
                """
                INSERT INTO payment_ledger.financial_transactions
                    ("Id", "Type", "Status", "SourceAccountId", "DestinationAccountId",
                     "Amount", "Currency", "PostedOnUtc")
                VALUES (@transaction, 'InternalTransfer', 'Posted', @source, @destination,
                        10, 'VND', now());
                INSERT INTO payment_ledger.journals ("Id", "TransactionId", "PostedOnUtc")
                VALUES (@journal, @transaction, now());
                INSERT INTO payment_ledger.journal_entries
                    ("Id", "JournalId", "LedgerAccountId", "Direction", "Amount", "Currency")
                VALUES
                    (@debit, @journal, @source, 'Debit', 1, 'VND'),
                    (@credit, @journal, @destination, 'Credit', 1, 'VND');
                """,
                connection,
                transaction
            );
            mismatched.Parameters.AddWithValue("transaction", transactionId);
            mismatched.Parameters.AddWithValue("journal", journalId);
            mismatched.Parameters.AddWithValue("debit", Guid.NewGuid());
            mismatched.Parameters.AddWithValue("credit", Guid.NewGuid());
            mismatched.Parameters.AddWithValue("source", source);
            mismatched.Parameters.AddWithValue("destination", destination);
            await mismatched.ExecuteNonQueryAsync();
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(
                () => transaction.CommitAsync()
            );
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }

        await using (NpgsqlCommand invalidDirection = new(
            """
            INSERT INTO payment_ledger.journal_entries
                ("Id", "JournalId", "LedgerAccountId", "Direction", "Amount", "Currency")
            SELECT gen_random_uuid(), "Id", @source, 'Bogus', 1, 'VND'
            FROM payment_ledger.journals LIMIT 1;
            """,
            connection
        ))
        {
            invalidDirection.Parameters.AddWithValue("source", source);
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(
                () => invalidDirection.ExecuteNonQueryAsync()
            );
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }

        await using (NpgsqlCommand mutateTransaction = new(
            """
            UPDATE payment_ledger.financial_transactions
            SET "Amount" = "Amount" + 1
            WHERE "Id" = @id;
            """,
            connection
        ))
        {
            mutateTransaction.Parameters.AddWithValue("id", postedTransactionId);
            PostgresException ex = await Assert.ThrowsAsync<PostgresException>(
                () => mutateTransaction.ExecuteNonQueryAsync()
            );
            Assert.Equal("55000", ex.SqlState);
        }
    }

    private static async Task CreateDatabaseAsync(string adminConnection, string databaseName)
    {
        await using NpgsqlConnection connection = new(adminConnection);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string adminConnection, string databaseName)
    {
        await using NpgsqlConnection connection = new(adminConnection);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",
            connection
        );
        await command.ExecuteNonQueryAsync();
    }

    private sealed record TestActor(string UserId) : IPaymentLedgerActor
    {
        public string TraceId => "postgres-test-trace";
    }
}
