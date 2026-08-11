using Friday.Modules.Customer.Domain.Auditing;
using Friday.Modules.Customer.Domain.Customers;
using Friday.Modules.Customer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using CustomerAggregate = Friday.Modules.Customer.Domain.Customers.Customer;
using Friday.BuildingBlocks.Application.Abstractions;
using Friday.BuildingBlocks.Application.Behaviors;
using Friday.BuildingBlocks.Domain.Entities;
using Friday.Modules.Customer.Application.Persistence;
using Friday.Modules.Customer.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.Modules.Customer.PostgreSqlTests;

public sealed class CustomerPostgreSqlVerificationTests
{
    [Fact]
    public async Task Customer_relational_invariants_hold_on_clean_postgresql_database()
    {
        string adminConnection = Environment.GetEnvironmentVariable("FRIDAY_CUSTOMER_TEST_PG")
            ?? "Host=127.0.0.1;Port=5432;Database=postgres;Username=friday;Password=friday;Timeout=15";
        string suffix = Guid.NewGuid().ToString("N");
        string databaseName = $"friday_customer_it_{suffix}";
        string runtimeRole = $"friday_cust_runtime_{suffix}";
        string retentionRole = $"friday_cust_retention_{suffix}";
        string rolePassword = $"Cust_{Guid.NewGuid():N}!Aa1";
        Assert.Matches("^[a-z0-9_]+$", databaseName);

        await CreateDatabaseAsync(adminConnection, databaseName);
        string databaseConnection = new NpgsqlConnectionStringBuilder(adminConnection)
        {
            Database = databaseName,
        }.ConnectionString;

        try
        {
            DbContextOptions<CustomerDbContext> options = CreateOptions(databaseConnection);
            await using (CustomerDbContext migrationContext = new(options))
                await migrationContext.Database.MigrateAsync();
            RoleConnections roles = await ProvisionRolesAsync(
                adminConnection,
                databaseConnection,
                runtimeRole,
                retentionRole,
                rolePassword
            );

            await VerifyCleanMigrationAsync(databaseConnection);
            await VerifyUniqueDocumentsAsync(options);
            await VerifyConcurrentDuplicateRaceAsync(options);
            await VerifyOptimisticConcurrencyAsync(options);
            await VerifyAuditFailureRollsBackMutationAsync(options);
            await VerifyTransactionPipelineRollsBackAuditFailureAsync(databaseConnection);
            await VerifyAuditAppendOnlyAndRetentionAsync(databaseConnection, roles, options);
            await VerifyIndexesAndPlansAsync(databaseConnection, options);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabaseAsync(adminConnection, databaseName);
            await DropRoleAsync(adminConnection, runtimeRole);
            await DropRoleAsync(adminConnection, retentionRole);
        }
    }

    private static DbContextOptions<CustomerDbContext> CreateOptions(string connectionString)
    {
        DbContextOptionsBuilder<CustomerDbContext> builder = new();
        builder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(CustomerDbContext).Assembly.FullName);
            npgsql.MigrationsHistoryTable(
                CustomerDbContext.MigrationHistoryTableName,
                CustomerDbContext.SchemaName
            );
        });
        return builder.Options;
    }

    private static async Task VerifyCleanMigrationAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT
              (SELECT count(*) FROM customer."__EFMigrationsHistory"),
              to_regclass('customer.customers') IS NOT NULL,
              to_regclass('customer.customer_change_audits') IS NOT NULL;
            """,
            connection
        );
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
    }

    private static async Task VerifyUniqueDocumentsAsync(DbContextOptions<CustomerDbContext> options)
    {
        CitizenDocument cccd = CitizenDocument.Create(
            CitizenDocumentType.VietnamCitizenId, "VN", "001234567890"
        );
        await InsertCustomerAsync(options, "CUS_UNIQUE_CCCD_01", cccd);
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            InsertCustomerAsync(options, "CUS_UNIQUE_CCCD_02", cccd)
        );

        CitizenDocument passport = CitizenDocument.Create(
            CitizenDocumentType.Passport, "FR", "12AB3456"
        );
        await InsertCustomerAsync(options, "CUS_UNIQUE_PASS_01", passport);
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            InsertCustomerAsync(options, "CUS_UNIQUE_PASS_02", passport)
        );

        await InsertCustomerAsync(
            options,
            "CUS_COUNTRY_PASS_01",
            CitizenDocument.Create(CitizenDocumentType.Passport, "CA", "12AB3456")
        );

        await using CustomerDbContext invalid = new(options);
        await Assert.ThrowsAsync<PostgresException>(() => invalid.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO customer.customers
            ("CustomerCode", "FullName", "CitizenDocumentType", "CitizenIssuingCountryCode",
             "CitizenDocumentNumber", "CitizenIdMasked", "Status", "OpenedOnUtc", "Version",
             "CreatedOnUtc", "UpdatedOnUtc")
            VALUES
            ('CUS_INVALID_DB_001', 'Invalid', 'VietnamCitizenId', 'VN', 'ABCDEFGHIJKL',
             '********IJKL', 'Active', now(), 0, now(), now())
            """
        ));
    }

    private static async Task VerifyConcurrentDuplicateRaceAsync(
        DbContextOptions<CustomerDbContext> options
    )
    {
        CitizenDocument document = CitizenDocument.Create(
            CitizenDocumentType.Passport, "CA", "RACE12345"
        );
        await using CustomerDbContext first = new(options);
        await using CustomerDbContext second = new(options);
        first.Customers.Add(NewCustomer("CUS_RACE_DUPLICATE1", document));
        second.Customers.Add(NewCustomer("CUS_RACE_DUPLICATE2", document));

        Task<Exception?> firstWrite = CaptureAsync(() => first.SaveChangesAsync());
        Task<Exception?> secondWrite = CaptureAsync(() => second.SaveChangesAsync());
        Exception?[] outcomes = await Task.WhenAll(firstWrite, secondWrite);

        Assert.Single(outcomes, x => x is null);
        Assert.Single(outcomes, x => x is DbUpdateException);
    }

    private static async Task VerifyOptimisticConcurrencyAsync(
        DbContextOptions<CustomerDbContext> options
    )
    {
        CitizenDocument document = CitizenDocument.Create(
            CitizenDocumentType.Passport, "US", "CONCUR123"
        );
        int id = await InsertCustomerAsync(options, "CUS_CONCURRENCY_001", document);

        await using CustomerDbContext first = new(options);
        await using CustomerDbContext second = new(options);
        CustomerAggregate firstCustomer = await first.Customers.SingleAsync(x => x.Id == id);
        CustomerAggregate secondCustomer = await second.Customers.SingleAsync(x => x.Id == id);
        firstCustomer.UpdateProfile("First Writer", null, ExistingDocument(firstCustomer));
        secondCustomer.UpdateProfile("Second Writer", null, ExistingDocument(secondCustomer));

        Exception?[] outcomes = await Task.WhenAll(
            CaptureAsync(() => first.SaveChangesAsync()),
            CaptureAsync(() => second.SaveChangesAsync())
        );
        Assert.Single(outcomes, x => x is null);
        Assert.Single(outcomes, x => x is DbUpdateConcurrencyException);
    }

    private static async Task VerifyAuditFailureRollsBackMutationAsync(
        DbContextOptions<CustomerDbContext> options
    )
    {
        const string customerCode = "CUS_AUDIT_ROLLBACK1";
        await using (CustomerDbContext context = new(options))
        await using (Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync())
        {
            CustomerAggregate customer = NewCustomer(
                customerCode,
                CitizenDocument.Create(CitizenDocumentType.Passport, "DE", "ROLLBACK1")
            );
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            context.CustomerChangeAudits.Add(CustomerChangeAudit.Create(
                customer,
                "CUSTOMER_CREATED",
                "operator-test",
                CustomerAuditChangeSet.Created(customer),
                null,
                CustomerStatus.Active.ToString(),
                new string('x', 501),
                "SUCCESS",
                "trace-test",
                DateTime.UtcNow,
                DateTime.UtcNow.AddDays(365)
            ));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            await transaction.RollbackAsync();
        }

        await using CustomerDbContext verification = new(options);
        Assert.False(await verification.Customers.AnyAsync(x => x.CustomerCode == customerCode));
    }

    private static async Task VerifyAuditAppendOnlyAndRetentionAsync(
        string connectionString,
        RoleConnections roles,
        DbContextOptions<CustomerDbContext> options
    )
    {
        DateTime now = DateTime.UtcNow;
        await using (CustomerDbContext context = new(options))
        {
            CustomerAggregate customer = NewCustomer(
                "CUS_RETENTION_00001",
                CitizenDocument.Create(CitizenDocumentType.Passport, "JP", "RETENTION1")
            );
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            for (int index = 1; index <= 3; index++)
                context.CustomerChangeAudits.Add(CreateAudit(
                    customer,
                    $"trace-retention-{index}",
                    now.AddDays(-(index + 1)),
                    now.AddDays(-index)
                ));
            context.CustomerChangeAudits.Add(CreateAudit(
                customer,
                "trace-retention-future",
                now,
                now.AddDays(1)
            ));
            await context.SaveChangesAsync();
        }

        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand("DELETE FROM customer.customer_change_audits", connection).ExecuteNonQueryAsync()
        );

        await using NpgsqlConnection runtime = new(roles.RuntimeConnectionString);
        await runtime.OpenAsync();
        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand("DELETE FROM customer.customer_change_audits", runtime).ExecuteNonQueryAsync()
        );
        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand(
                "UPDATE customer.customer_change_audits SET \"Outcome\" = 'CHANGED'",
                runtime
            ).ExecuteNonQueryAsync()
        );
        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand("SELECT customer.purge_expired_customer_audits(100)", runtime).ExecuteScalarAsync()
        );

        await using NpgsqlConnection retentionOne = new(roles.RetentionConnectionString);
        await using NpgsqlConnection retentionTwo = new(roles.RetentionConnectionString);
        await retentionOne.OpenAsync();
        await retentionTwo.OpenAsync();
        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand("DELETE FROM customer.customer_change_audits", retentionOne).ExecuteNonQueryAsync()
        );
        await Assert.ThrowsAsync<PostgresException>(() =>
            new NpgsqlCommand("SELECT customer.purge_expired_customer_audits(0)", retentionOne).ExecuteScalarAsync()
        );

        Task<int> firstPurge = ExecutePurgeAsync(retentionOne, 2);
        Task<int> secondPurge = ExecutePurgeAsync(retentionTwo, 2);
        int[] purged = await Task.WhenAll(firstPurge, secondPurge);
        Assert.Equal(3, purged.Sum());

        await using NpgsqlCommand remaining = new(
            "SELECT count(*) FROM customer.customer_change_audits",
            connection
        );
        Assert.Equal(1L, Convert.ToInt64(await remaining.ExecuteScalarAsync()));
    }

    private static async Task VerifyTransactionPipelineRollsBackAuditFailureAsync(
        string connectionString
    )
    {
        const string customerCode = "CUS_PIPELINE_ROLLBACK";
        ConfigurationManager configuration = new();
        configuration["ConnectionStrings:CustomerDb"] = connectionString;
        ServiceCollection services = new();
        services.AddCustomerInfrastructure(configuration);
        services.AddScoped<IDomainEventDispatcher, NoOpDomainEventDispatcher>();

        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ICustomerUnitOfWork unitOfWork =
            scope.ServiceProvider.GetRequiredService<ICustomerUnitOfWork>();
        CustomerDbContext db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        TransactionBehavior<PipelineProbeCommand, bool> behavior =
            new(new FixedUnitOfWorkResolver(unitOfWork));

        await Assert.ThrowsAsync<DbUpdateException>(() => behavior.HandleAsync(
            new PipelineProbeCommand(),
            async () =>
            {
                CustomerAggregate customer = NewCustomer(
                    customerCode,
                    CitizenDocument.Create(CitizenDocumentType.Passport, "AU", "PIPELINE1")
                );
                db.Customers.Add(customer);
                await unitOfWork.FlushAsync();
                db.CustomerChangeAudits.Add(CustomerChangeAudit.Create(
                    customer,
                    "CUSTOMER_CREATED",
                    "operator-test",
                    CustomerAuditChangeSet.Created(customer),
                    null,
                    CustomerStatus.Active.ToString(),
                    new string('x', 501),
                    "SUCCESS",
                    "trace-pipeline",
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(365)
                ));
                return true;
            },
            CancellationToken.None
        ));

        await using CustomerDbContext verification = new(CreateOptions(connectionString));
        Assert.False(await verification.Customers.AnyAsync(x => x.CustomerCode == customerCode));
    }

    private static CustomerChangeAudit CreateAudit(
        CustomerAggregate customer,
        string traceId,
        DateTime occurredOnUtc,
        DateTime retainUntilUtc
    ) => CustomerChangeAudit.Create(
        customer,
        "CUSTOMER_CREATED",
        "operator-test",
        CustomerAuditChangeSet.Created(customer),
        null,
        CustomerStatus.Active.ToString(),
        null,
        "SUCCESS",
        traceId,
        occurredOnUtc,
        retainUntilUtc
    );

    private static async Task<int> ExecutePurgeAsync(NpgsqlConnection connection, int batchSize)
    {
        await using NpgsqlCommand command = new(
            "SELECT customer.purge_expired_customer_audits(@batch_size)",
            connection
        );
        command.Parameters.AddWithValue("batch_size", batchSize);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task VerifyIndexesAndPlansAsync(
        string connectionString,
        DbContextOptions<CustomerDbContext> options
    )
    {
        await using (CustomerDbContext seed = new(options))
        {
            for (int i = 0; i < 1_000; i++)
            {
                CustomerAggregate customer = NewCustomer(
                    $"CUS_PLAN_{i:0000000000}",
                    CitizenDocument.Create(CitizenDocumentType.Passport, "GB", $"PLAN{i:000000}")
                );
                if (i < 50) customer.Suspend("plan selectivity");
                seed.Customers.Add(customer);
            }
            await seed.SaveChangesAsync();
        }

        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await new NpgsqlCommand("ANALYZE customer.customers;", connection)
            .ExecuteNonQueryAsync();

        string byCodePlan = await ExplainAsync(
            connection,
            "SELECT \"Id\" FROM customer.customers WHERE \"CustomerCode\" = 'CUS_PLAN_0000000000'"
        );
        string statusPlan = await ExplainAsync(
            connection,
            "SELECT \"Id\" FROM customer.customers WHERE \"Status\" = 'Suspended' ORDER BY \"OpenedOnUtc\", \"Id\" LIMIT 50"
        );
        Assert.Contains("IX_customers_CustomerCode", byCodePlan, StringComparison.Ordinal);
        Assert.Contains("IX_customers_Status_OpenedOnUtc_Id", statusPlan, StringComparison.Ordinal);
    }

    private static async Task<string> ExplainAsync(NpgsqlConnection connection, string sql)
    {
        await using NpgsqlCommand command = new(
            $"EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) {sql}",
            connection
        );
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        List<string> lines = [];
        while (await reader.ReadAsync()) lines.Add(reader.GetString(0));
        return string.Join(Environment.NewLine, lines);
    }

    private static async Task<int> InsertCustomerAsync(
        DbContextOptions<CustomerDbContext> options,
        string code,
        CitizenDocument document
    )
    {
        await using CustomerDbContext context = new(options);
        CustomerAggregate customer = NewCustomer(code, document);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer.Id;
    }

    private static CustomerAggregate NewCustomer(string code, CitizenDocument document) =>
        CustomerAggregate.Create(code, "PostgreSQL Test Customer", null, document, DateTime.UtcNow);

    private static CitizenDocument ExistingDocument(CustomerAggregate customer) =>
        CitizenDocument.Create(
            customer.CitizenDocumentType,
            customer.CitizenIssuingCountryCode,
            customer.CitizenDocumentNumber
        );

    private static async Task<Exception?> CaptureAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task CreateDatabaseAsync(string adminConnection, string databaseName)
    {
        await using NpgsqlConnection connection = new(adminConnection);
        await connection.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection).ExecuteNonQueryAsync();
    }

    private static async Task<RoleConnections> ProvisionRolesAsync(
        string adminConnection,
        string databaseConnection,
        string runtimeRole,
        string retentionRole,
        string password
    )
    {
        await using (NpgsqlConnection admin = new(adminConnection))
        {
            await admin.OpenAsync();
            string passwordLiteral = password.Replace("'", "''", StringComparison.Ordinal);
            await new NpgsqlCommand(
                $"CREATE ROLE \"{runtimeRole}\" LOGIN PASSWORD '{passwordLiteral}'",
                admin
            ).ExecuteNonQueryAsync();
            await new NpgsqlCommand(
                $"CREATE ROLE \"{retentionRole}\" LOGIN PASSWORD '{passwordLiteral}'",
                admin
            ).ExecuteNonQueryAsync();
        }

        await using (NpgsqlConnection database = new(databaseConnection))
        {
            await database.OpenAsync();
            string grants = $"""
                REVOKE ALL ON SCHEMA customer FROM PUBLIC;
                GRANT USAGE ON SCHEMA customer TO "{runtimeRole}", "{retentionRole}";
                GRANT SELECT, INSERT, UPDATE ON customer.customers TO "{runtimeRole}";
                GRANT SELECT, INSERT ON customer.customer_change_audits TO "{runtimeRole}";
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA customer TO "{runtimeRole}";
                GRANT SELECT ON customer."__EFMigrationsHistory" TO "{runtimeRole}";
                GRANT EXECUTE ON FUNCTION customer.purge_expired_customer_audits(integer)
                    TO "{retentionRole}";
                """;
            await new NpgsqlCommand(grants, database).ExecuteNonQueryAsync();
        }

        NpgsqlConnectionStringBuilder runtime = new(databaseConnection)
        {
            Username = runtimeRole,
            Password = password,
        };
        NpgsqlConnectionStringBuilder retention = new(databaseConnection)
        {
            Username = retentionRole,
            Password = password,
        };
        return new RoleConnections(runtime.ConnectionString, retention.ConnectionString);
    }

    private static async Task DropDatabaseAsync(string adminConnection, string databaseName)
    {
        Assert.StartsWith("friday_customer_it_", databaseName, StringComparison.Ordinal);
        await using NpgsqlConnection connection = new(adminConnection);
        await connection.OpenAsync();
        await using NpgsqlCommand terminate = new(
            """
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = @databaseName AND pid <> pg_backend_pid();
            """,
            connection
        );
        terminate.Parameters.AddWithValue("databaseName", databaseName);
        await terminate.ExecuteNonQueryAsync();
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\"", connection)
            .ExecuteNonQueryAsync();
    }

    private static async Task DropRoleAsync(string adminConnection, string roleName)
    {
        Assert.Matches("^friday_cust_(runtime|retention)_[a-f0-9]{32}$", roleName);
        await using NpgsqlConnection connection = new(adminConnection);
        await connection.OpenAsync();
        await new NpgsqlCommand($"DROP ROLE IF EXISTS \"{roleName}\"", connection)
            .ExecuteNonQueryAsync();
    }

    private sealed record RoleConnections(
        string RuntimeConnectionString,
        string RetentionConnectionString
    );

    private sealed record PipelineProbeCommand : ICustomerCommand<bool>;

    private sealed class FixedUnitOfWorkResolver(IUnitOfWork unitOfWork) : IUnitOfWorkResolver
    {
        public IUnitOfWork Resolve(object request) => unitOfWork;
    }

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(
            IReadOnlyCollection<Entity> entities,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }
}
