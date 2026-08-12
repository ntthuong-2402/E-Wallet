using Friday.Modules.PaymentLedger.Domain.Ledger;
using Microsoft.EntityFrameworkCore;

namespace Friday.Modules.PaymentLedger.Infrastructure.Persistence;

public sealed class PaymentLedgerDbContext(DbContextOptions<PaymentLedgerDbContext> options) : DbContext(options)
{
    public const string SchemaName = "payment_ledger";
    public const string MigrationHistoryTableName = "__EFMigrationsHistory";
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<Journal> Journals => Set<Journal>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<FinancialAuditRecord> FinancialAuditRecords => Set<FinancialAuditRecord>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) { EnsurePostedLedgerIsImmutable(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) { EnsurePostedLedgerIsImmutable(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
    protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.HasDefaultSchema(SchemaName); modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentLedgerDbContext).Assembly); }
    private void EnsurePostedLedgerIsImmutable()
    {
        if (ChangeTracker.Entries<Journal>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) || ChangeTracker.Entries<JournalEntry>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Posted journals and journal entries are append-only.");
        if (ChangeTracker.Entries<FinancialTransaction>().Any(x => x.State == EntityState.Deleted))
            throw new InvalidOperationException("Financial transactions cannot be deleted.");
        if (ChangeTracker.Entries<FinancialAuditRecord>()
            .Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Financial audit records are append-only.");
        foreach (var entry in ChangeTracker.Entries<FinancialTransaction>()
            .Where(x => x.State == EntityState.Modified))
        {
            bool validReversalTransition =
                entry.OriginalValues.GetValue<FinancialTransactionType>(nameof(FinancialTransaction.Type))
                    == FinancialTransactionType.InternalTransfer
                && entry.OriginalValues.GetValue<FinancialTransactionStatus>(nameof(FinancialTransaction.Status))
                    == FinancialTransactionStatus.Posted
                && entry.CurrentValues.GetValue<FinancialTransactionStatus>(nameof(FinancialTransaction.Status))
                    == FinancialTransactionStatus.Reversed
                && entry.OriginalValues.GetValue<Guid?>(nameof(FinancialTransaction.ReversalTransactionId)) is null
                && entry.CurrentValues.GetValue<Guid?>(nameof(FinancialTransaction.ReversalTransactionId)) is not null
                && entry.Properties.Where(property => property.IsModified).All(property =>
                    property.Metadata.Name is nameof(FinancialTransaction.Status)
                        or nameof(FinancialTransaction.ReversalTransactionId));
            if (!validReversalTransition)
                throw new InvalidOperationException(
                    "Posted transaction economics are immutable; only the reversal transition is allowed."
                );
        }
    }
}
