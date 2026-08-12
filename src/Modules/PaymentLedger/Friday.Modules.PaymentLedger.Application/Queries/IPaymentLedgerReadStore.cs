using Friday.Modules.PaymentLedger.Application.Models;

namespace Friday.Modules.PaymentLedger.Application.Queries;

public interface IPaymentLedgerReadStore
{
    Task<LedgerAccountDto?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TransactionDto?> GetTransactionAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );
    Task<TransactionDto?> GetTransactionByReferenceAsync(
        string actor,
        string operation,
        string refId,
        CancellationToken cancellationToken = default
    );
    Task<TransactionSearchPage> SearchAsync(
        TransactionSearchCriteria criteria,
        CancellationToken cancellationToken = default
    );
}
