using Friday.BuildingBlocks.Application.Abstractions;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.BuildingBlocks.Domain.Entities;
using Friday.BuildingBlocks.Infrastructure;
using Friday.Modules.PaymentLedger.Application.Actors;
using Friday.Modules.PaymentLedger.Application;
using Friday.Modules.PaymentLedger.Application.Features;
using Friday.Modules.PaymentLedger.Application.Models;
using Friday.Modules.PaymentLedger.Application.Persistence;
using Friday.Modules.PaymentLedger.Domain;
using Friday.Modules.PaymentLedger.Domain.Ledger;
using Friday.Modules.PaymentLedger.Infrastructure;
using Friday.Modules.PaymentLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Friday.Modules.PaymentLedger.UnitTests;

public sealed class PaymentLedgerTests
{
    private static readonly DateTime Now = new(2026, 8, 12, 1, 2, 3, DateTimeKind.Utc);

    [Fact]
    public void Posting_MutatesBalancesAndCreatesBalancedImmutableJournal()
    {
        LedgerAccount source = Funded("SRC", 1_000); LedgerAccount destination = LedgerAccount.Open("DST", Now);
        FinancialTransaction transaction = FinancialTransaction.PostInternalTransfer(source, destination, 250, "VND", "test", Now);
        Assert.Equal(750, source.AvailableBalance); Assert.Equal(250, destination.AvailableBalance);
        Assert.Equal(FinancialTransactionStatus.Posted, transaction.Status); Assert.Equal(2, transaction.Journal.Entries.Count);
        Assert.Equal(transaction.Journal.Entries.Where(x => x.Direction == LedgerEntryDirection.Debit).Sum(x => x.Amount), transaction.Journal.Entries.Where(x => x.Direction == LedgerEntryDirection.Credit).Sum(x => x.Amount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.5)]
    public void Posting_RejectsInvalidVndAmount(decimal amount)
    {
        LedgerAccount source = Funded("SRC", 1_000); LedgerAccount destination = LedgerAccount.Open("DST", Now);
        Assert.ThrowsAny<ArgumentException>(() => FinancialTransaction.PostInternalTransfer(source, destination, amount, "VND", null, Now));
    }

    [Fact]
    public void Posting_RejectsSameAccountAndInsufficientFunds()
    {
        LedgerAccount source = Funded("SRC", 100);
        Assert.Throws<ArgumentException>(() => FinancialTransaction.PostInternalTransfer(source, source, 10, "VND", null, Now));
        Assert.Throws<InvalidOperationException>(() => FinancialTransaction.PostInternalTransfer(source, LedgerAccount.Open("DST", Now), 101, "VND", null, Now));
    }

    [Fact]
    public void Reversal_IsCompensatingAndFullOnly()
    {
        LedgerAccount source = Funded("SRC", 1_000); LedgerAccount destination = LedgerAccount.Open("DST", Now);
        FinancialTransaction original = FinancialTransaction.PostInternalTransfer(source, destination, 250, "VND", null, Now);
        FinancialTransaction reversal = original.Reverse(source, destination, "operator correction", Now.AddMinutes(1));
        Assert.Equal(1_000, source.AvailableBalance); Assert.Equal(0, destination.AvailableBalance);
        Assert.Equal(FinancialTransactionStatus.Reversed, original.Status); Assert.Equal(original.Id, reversal.OriginalTransactionId);
        Assert.Equal(reversal.Id, reversal.Journal.TransactionId);
    }

    [Fact]
    public void Reversal_RejectsWhenBeneficiarySpentFunds()
    {
        LedgerAccount source = Funded("SRC", 1_000); LedgerAccount destination = LedgerAccount.Open("DST", Now);
        FinancialTransaction original = FinancialTransaction.PostInternalTransfer(source, destination, 250, "VND", null, Now);
        destination.Debit(1, Now.AddSeconds(1));
        Assert.Throws<InvalidOperationException>(() => original.Reverse(source, destination, "reverse", Now.AddMinutes(1)));
    }

    [Fact]
    public void PaymentLedgerCommand_SelectsKeyedUnitOfWorkAndAssembliesStayIndependent()
    {
        IUnitOfWorkCommand command = new OpenLedgerAccountCommand("ref-1", "account-1");
        Assert.Equal(PaymentLedgerPersistence.UnitOfWorkKey, command.UnitOfWorkKey);
        IEnumerable<string> refs = new[] { typeof(PaymentLedgerDomainAssemblyMarker).Assembly, typeof(Friday.Modules.PaymentLedger.Application.PaymentLedgerApplicationAssemblyMarker).Assembly, typeof(PaymentLedgerDbContext).Assembly }.SelectMany(x => x.GetReferencedAssemblies()).Select(x => x.Name ?? "");
        Assert.DoesNotContain(refs, x => x.StartsWith("Friday.Modules.Customer", StringComparison.Ordinal) || x.StartsWith("Friday.Modules.Admin", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DurableIdempotency_ReplaysAndRejectsChangedPayload()
    {
        await using ServiceProvider provider = BuildProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        OpenLedgerAccountHandler handler = ActivatorUtilities.CreateInstance<OpenLedgerAccountHandler>(scope.ServiceProvider);
        LedgerAccountDto first = await handler.HandleAsync(new("ref-1", "account-1"), default);
        await scope.ServiceProvider.GetRequiredService<IPaymentLedgerUnitOfWork>().CommitAsync();
        LedgerAccountDto replay = await handler.HandleAsync(new("ref-1", "account-1"), default);
        Assert.Equal(first, replay);
        FridayException conflict = await Assert.ThrowsAsync<FridayException>(() => handler.HandleAsync(new("ref-1", "changed"), default));
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task DbContext_BlocksPostedJournalMutationAndDeletion()
    {
        DbContextOptions<PaymentLedgerDbContext> options = new DbContextOptionsBuilder<PaymentLedgerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using PaymentLedgerDbContext db = new(options);
        LedgerAccount source = Funded("SRC", 100); LedgerAccount destination = LedgerAccount.Open("DST", Now);
        FinancialTransaction transaction = FinancialTransaction.PostInternalTransfer(source, destination, 10, "VND", null, Now);
        db.AddRange(source, destination, transaction); await db.SaveChangesAsync();
        db.Entry(transaction.Journal.Entries.First()).State = EntityState.Deleted;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static LedgerAccount Funded(string reference, decimal amount) { LedgerAccount account = LedgerAccount.Open(reference, Now); account.Credit(amount, Now); return account; }
    private static ServiceProvider BuildProvider()
    {
        ConfigurationManager config = new(); config["Database:InMemoryDatabaseName"] = Guid.NewGuid().ToString();
        ServiceCollection services = new(); services.AddBuildingBlocksInfrastructure(config); services.AddPaymentLedgerApplication(); services.AddPaymentLedgerInfrastructure(config); services.AddScoped<IDomainEventDispatcher, NoOpDispatcher>(); services.AddScoped<IPaymentLedgerActor>(_ => new Actor("operator")); return services.BuildServiceProvider();
    }
    private sealed record Actor(string UserId) : IPaymentLedgerActor
    {
        public string TraceId => "unit-test-trace";
    }
    private sealed class NoOpDispatcher : IDomainEventDispatcher { public Task DispatchAsync(IReadOnlyCollection<Entity> entities, CancellationToken cancellationToken = default) => Task.CompletedTask; }
}
