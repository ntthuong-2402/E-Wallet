using Friday.Modules.PaymentLedger.Domain.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friday.Modules.PaymentLedger.Infrastructure.Persistence.Configurations;

public sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> b)
    {
        b.ToTable("ledger_accounts", PaymentLedgerDbContext.SchemaName, t => { t.HasCheckConstraint("CK_ledger_accounts_currency", "\"Currency\" = 'VND'"); t.HasCheckConstraint("CK_ledger_accounts_balance", "\"AvailableBalance\" >= 0 AND \"AvailableBalance\" = trunc(\"AvailableBalance\")"); t.HasCheckConstraint("CK_ledger_accounts_status", "\"Status\" IN ('Active','Suspended')"); });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.AccountRef).HasMaxLength(100).IsRequired(); b.HasIndex(x => x.AccountRef).IsUnique();
        b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength().IsRequired(); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.AvailableBalance).HasPrecision(19, 0).IsRequired(); b.Property(x => x.Version).IsConcurrencyToken().IsRequired();
        b.Property(x => x.CreatedOnUtc).HasColumnType("timestamp with time zone").IsRequired(); b.Property(x => x.UpdatedOnUtc).HasColumnType("timestamp with time zone").IsRequired();
    }
}
public sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> b)
    {
        b.ToTable("financial_transactions", PaymentLedgerDbContext.SchemaName, t => { t.HasCheckConstraint("CK_financial_transactions_currency", "\"Currency\" = 'VND'"); t.HasCheckConstraint("CK_financial_transactions_amount", "\"Amount\" > 0 AND \"Amount\" = trunc(\"Amount\")"); t.HasCheckConstraint("CK_financial_transactions_accounts", "\"SourceAccountId\" <> \"DestinationAccountId\""); t.HasCheckConstraint("CK_financial_transactions_type", "\"Type\" IN ('InternalTransfer','Reversal')"); t.HasCheckConstraint("CK_financial_transactions_status", "\"Status\" IN ('Posted','Reversed')"); });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired(); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired(); b.Property(x => x.Amount).HasPrecision(19, 0).IsRequired(); b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength().IsRequired(); b.Property(x => x.Description).HasMaxLength(500); b.Property(x => x.PostedOnUtc).HasColumnType("timestamp with time zone").IsRequired();
        b.HasIndex(x => new { x.PostedOnUtc, x.Id }); b.HasIndex(x => new { x.SourceAccountId, x.PostedOnUtc }); b.HasIndex(x => new { x.DestinationAccountId, x.PostedOnUtc }); b.HasIndex(x => x.OriginalTransactionId).IsUnique().HasFilter("\"OriginalTransactionId\" IS NOT NULL");
        b.HasOne<LedgerAccount>().WithMany().HasForeignKey(x => x.SourceAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LedgerAccount>().WithMany().HasForeignKey(x => x.DestinationAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FinancialTransaction>().WithOne().HasForeignKey<FinancialTransaction>(x => x.OriginalTransactionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FinancialTransaction>().WithOne().HasForeignKey<FinancialTransaction>(x => x.ReversalTransactionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Journal).WithOne().HasForeignKey<Journal>(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class JournalConfiguration : IEntityTypeConfiguration<Journal>
{
    public void Configure(EntityTypeBuilder<Journal> b) { b.ToTable("journals", PaymentLedgerDbContext.SchemaName); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.TransactionId).IsRequired(); b.Property(x => x.PostedOnUtc).HasColumnType("timestamp with time zone").IsRequired(); b.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.JournalId).OnDelete(DeleteBehavior.Restrict); b.Navigation(x => x.Entries).UsePropertyAccessMode(PropertyAccessMode.Field); }
}
public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> b) { b.ToTable("journal_entries", PaymentLedgerDbContext.SchemaName, t => { t.HasCheckConstraint("CK_journal_entries_currency", "\"Currency\" = 'VND'"); t.HasCheckConstraint("CK_journal_entries_amount", "\"Amount\" > 0 AND \"Amount\" = trunc(\"Amount\")"); t.HasCheckConstraint("CK_journal_entries_direction", "\"Direction\" IN ('Debit','Credit')"); }); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.JournalId).IsRequired(); b.Property(x => x.LedgerAccountId).IsRequired(); b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(8).IsRequired(); b.Property(x => x.Amount).HasPrecision(19, 0).IsRequired(); b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength().IsRequired(); b.HasIndex(x => new { x.LedgerAccountId, x.JournalId }); b.HasOne<LedgerAccount>().WithMany().HasForeignKey(x => x.LedgerAccountId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b) { b.ToTable("idempotency_records", PaymentLedgerDbContext.SchemaName); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedOnAdd(); b.Property(x => x.ActorUserId).HasMaxLength(128).IsRequired(); b.Property(x => x.Operation).HasMaxLength(64).IsRequired(); b.Property(x => x.RefId).HasMaxLength(100).IsRequired(); b.Property(x => x.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired(); b.Property(x => x.ResponseJson).HasColumnType("jsonb").IsRequired(); b.Property(x => x.CreatedOnUtc).HasColumnType("timestamp with time zone").IsRequired(); b.HasIndex(x => new { x.ActorUserId, x.Operation, x.RefId }).IsUnique(); }
}
public sealed class FinancialAuditRecordConfiguration : IEntityTypeConfiguration<FinancialAuditRecord>
{
    public void Configure(EntityTypeBuilder<FinancialAuditRecord> b)
    {
        b.ToTable("financial_audit_records", PaymentLedgerDbContext.SchemaName);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.ActorUserId).HasMaxLength(128).IsRequired();
        b.Property(x => x.Action).HasMaxLength(64).IsRequired();
        b.Property(x => x.RefId).HasMaxLength(100).IsRequired();
        b.Property(x => x.Outcome).HasMaxLength(16).IsRequired();
        b.Property(x => x.TraceId).HasMaxLength(128).IsRequired();
        b.Property(x => x.OccurredOnUtc).HasColumnType("timestamp with time zone").IsRequired();
        b.HasIndex(x => new { x.ActorUserId, x.OccurredOnUtc });
        b.HasIndex(x => new { x.FinancialTransactionId, x.OccurredOnUtc });
        b.HasIndex(x => new { x.LedgerAccountId, x.OccurredOnUtc });
    }
}
