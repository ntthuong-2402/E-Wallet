using Friday.Modules.PaymentLedger.Domain.Ledger;
using Friday.Modules.PaymentLedger.Domain.Repositories;
using Friday.Modules.PaymentLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Friday.Modules.PaymentLedger.Infrastructure.Repositories;

public sealed class LedgerAccountRepository(PaymentLedgerDbContext db) : ILedgerAccountRepository
{
    public Task<LedgerAccount?> GetAsync(Guid id, CancellationToken cancellationToken = default) => db.LedgerAccounts.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<LedgerAccount?> GetByRefAsync(string accountRef, CancellationToken cancellationToken = default) => db.LedgerAccounts.SingleOrDefaultAsync(x => x.AccountRef == accountRef, cancellationToken);
    public async Task<(LedgerAccount? First, LedgerAccount? Second)> GetForUpdateAsync(Guid firstId, Guid secondId, CancellationToken cancellationToken = default)
    {
        Guid low = firstId.CompareTo(secondId) < 0 ? firstId : secondId; Guid high = low == firstId ? secondId : firstId;
        List<LedgerAccount> values;
        if (db.Database.IsRelational()) values = await db.LedgerAccounts.FromSqlInterpolated($"SELECT * FROM payment_ledger.ledger_accounts WHERE \"Id\" IN ({low}, {high}) ORDER BY \"Id\" FOR UPDATE").ToListAsync(cancellationToken);
        else values = await db.LedgerAccounts.Where(x => x.Id == low || x.Id == high).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        return (values.ElementAtOrDefault(0), values.ElementAtOrDefault(1));
    }
    public Task AddAsync(LedgerAccount account, CancellationToken cancellationToken = default) => db.LedgerAccounts.AddAsync(account, cancellationToken).AsTask();
}
public sealed class FinancialTransactionRepository(PaymentLedgerDbContext db) : IFinancialTransactionRepository
{
    public Task<FinancialTransaction?> GetAsync(Guid id, CancellationToken cancellationToken = default) => db.FinancialTransactions.Include(x => x.Journal).ThenInclude(x => x.Entries).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task AddAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default) => db.FinancialTransactions.AddAsync(transaction, cancellationToken).AsTask();
}
public sealed class IdempotencyRepository(PaymentLedgerDbContext db) : IIdempotencyRepository
{
    public async Task AcquireAsync(string actor, string operation, string refId, CancellationToken cancellationToken = default) { if (db.Database.IsRelational()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor + "|" + operation + "|" + refId}, 0))", cancellationToken); }
    public Task<IdempotencyRecord?> GetAsync(string actor, string operation, string refId, CancellationToken cancellationToken = default) => db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor && x.Operation == operation && x.RefId == refId, cancellationToken);
    public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default) => db.IdempotencyRecords.AddAsync(record, cancellationToken).AsTask();
}
public sealed class FinancialAuditRepository(PaymentLedgerDbContext db) : IFinancialAuditRepository
{
    public Task AddAsync(
        FinancialAuditRecord record,
        CancellationToken cancellationToken = default
    ) => db.FinancialAuditRecords.AddAsync(record, cancellationToken).AsTask();
}
