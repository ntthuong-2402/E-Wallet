using Friday.Modules.PaymentLedger.Domain.Ledger;

namespace Friday.Modules.PaymentLedger.Domain.Repositories;

public interface ILedgerAccountRepository
{
    Task<LedgerAccount?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LedgerAccount?> GetByRefAsync(string accountRef, CancellationToken cancellationToken = default);
    Task<(LedgerAccount? First, LedgerAccount? Second)> GetForUpdateAsync(Guid firstId, Guid secondId, CancellationToken cancellationToken = default);
    Task AddAsync(LedgerAccount account, CancellationToken cancellationToken = default);
}
public interface IFinancialTransactionRepository
{
    Task<FinancialTransaction?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default);
}
public interface IIdempotencyRepository
{
    Task AcquireAsync(string actor, string operation, string refId, CancellationToken cancellationToken = default);
    Task<IdempotencyRecord?> GetAsync(string actor, string operation, string refId, CancellationToken cancellationToken = default);
    Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);
}

public interface IFinancialAuditRepository
{
    Task AddAsync(FinancialAuditRecord record, CancellationToken cancellationToken = default);
}
