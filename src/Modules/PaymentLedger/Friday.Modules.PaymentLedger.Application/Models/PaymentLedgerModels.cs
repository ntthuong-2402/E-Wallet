using Friday.Modules.PaymentLedger.Domain.Ledger;

namespace Friday.Modules.PaymentLedger.Application.Models;

public sealed record LedgerAccountDto(
    Guid Id,
    string AccountRef,
    string Currency,
    LedgerAccountStatus Status,
    decimal AvailableBalance,
    long Version,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc
)
{
    public static LedgerAccountDto From(LedgerAccount value) =>
        new(
            value.Id,
            value.AccountRef,
            value.Currency,
            value.Status,
            value.AvailableBalance,
            value.Version,
            value.CreatedOnUtc,
            value.UpdatedOnUtc
        );
}

public sealed record JournalEntryDto(
    Guid Id,
    Guid LedgerAccountId,
    LedgerEntryDirection Direction,
    decimal Amount,
    string Currency
);

public sealed record TransactionDto(
    Guid Id,
    FinancialTransactionType Type,
    FinancialTransactionStatus Status,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency,
    string? Description,
    Guid? OriginalTransactionId,
    Guid? ReversalTransactionId,
    DateTime PostedOnUtc,
    Guid JournalId,
    IReadOnlyList<JournalEntryDto> Entries
)
{
    public static TransactionDto From(FinancialTransaction value) =>
        new(
            value.Id,
            value.Type,
            value.Status,
            value.SourceAccountId,
            value.DestinationAccountId,
            value.Amount,
            value.Currency,
            value.Description,
            value.OriginalTransactionId,
            value.ReversalTransactionId,
            value.PostedOnUtc,
            value.Journal.Id,
            value
                .Journal.Entries.Select(x => new JournalEntryDto(
                    x.Id,
                    x.LedgerAccountId,
                    x.Direction,
                    x.Amount,
                    x.Currency
                ))
                .ToArray()
        );
}

public sealed record TransactionSearchPage(
    IReadOnlyList<TransactionDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record TransactionSearchCriteria(
    Guid? AccountId,
    string? Currency,
    FinancialTransactionType? Type,
    FinancialTransactionStatus? Status,
    DateTime? PostedFromUtc,
    DateTime? PostedToUtc,
    int Page,
    int PageSize
);
