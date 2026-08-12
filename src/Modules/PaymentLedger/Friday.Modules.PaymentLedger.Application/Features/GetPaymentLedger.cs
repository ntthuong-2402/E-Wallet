using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.PaymentLedger.Application.Actors;
using Friday.Modules.PaymentLedger.Application.Errors;
using Friday.Modules.PaymentLedger.Application.Models;
using Friday.Modules.PaymentLedger.Application.Queries;
using Friday.Modules.PaymentLedger.Domain.Ledger;
using LinKit.Core.Cqrs;

namespace Friday.Modules.PaymentLedger.Application.Features;

public sealed record GetLedgerAccountQuery(Guid AccountId) : IQuery<LedgerAccountDto>;

public sealed class GetLedgerAccountHandler(IPaymentLedgerReadStore store)
    : IQueryHandler<GetLedgerAccountQuery, LedgerAccountDto>
{
    public async Task<LedgerAccountDto> HandleAsync(
        GetLedgerAccountQuery request,
        CancellationToken cancellationToken
    ) =>
        await store.GetAccountAsync(request.AccountId, cancellationToken)
        ?? throw new FridayException(
            PaymentLedgerErrorCodes.AccountNotFound,
            "Ledger account was not found.",
            404
        );
}

public sealed record GetTransactionByIdQuery(Guid TransactionId) : IQuery<TransactionDto>;

public sealed class GetTransactionByIdHandler(IPaymentLedgerReadStore store)
    : IQueryHandler<GetTransactionByIdQuery, TransactionDto>
{
    public async Task<TransactionDto> HandleAsync(
        GetTransactionByIdQuery request,
        CancellationToken cancellationToken
    ) =>
        await store.GetTransactionAsync(request.TransactionId, cancellationToken)
        ?? throw new FridayException(
            PaymentLedgerErrorCodes.TransactionNotFound,
            "Transaction was not found.",
            404
        );
}

public sealed record GetTransactionByReferenceQuery(string Operation, string RefId)
    : IQuery<TransactionDto>;

public sealed class GetTransactionByReferenceHandler(
    IPaymentLedgerReadStore store,
    IPaymentLedgerActor actor
) : IQueryHandler<GetTransactionByReferenceQuery, TransactionDto>
{
    public async Task<TransactionDto> HandleAsync(
        GetTransactionByReferenceQuery request,
        CancellationToken cancellationToken
    )
    {
        if (
            request.Operation
            is not (
                CreateInternalTransferHandler.Operation
                or ReverseInternalTransferHandler.Operation
            )
        )
            throw new FridayException(
                PaymentLedgerErrorCodes.TransactionNotFound,
                "Transaction reference was not found.",
                404
            );
        return await store.GetTransactionByReferenceAsync(
                actor.UserId,
                request.Operation,
                IdempotencyRecord.NormalizeRefId(request.RefId),
                cancellationToken
            )
            ?? throw new FridayException(
                PaymentLedgerErrorCodes.TransactionNotFound,
                "Transaction reference was not found.",
                404
            );
    }
}

public sealed record SearchTransactionsQuery(
    Guid? AccountId = null,
    string? Currency = null,
    FinancialTransactionType? Type = null,
    FinancialTransactionStatus? Status = null,
    DateTime? PostedFromUtc = null,
    DateTime? PostedToUtc = null,
    int Page = 1,
    int PageSize = 50
) : IQuery<TransactionSearchPage>;

public sealed class SearchTransactionsHandler(IPaymentLedgerReadStore store)
    : IQueryHandler<SearchTransactionsQuery, TransactionSearchPage>
{
    public Task<TransactionSearchPage> HandleAsync(
        SearchTransactionsQuery request,
        CancellationToken cancellationToken
    )
    {
        if (
            request.PostedFromUtc is { Kind: not DateTimeKind.Utc }
            || request.PostedToUtc is { Kind: not DateTimeKind.Utc }
        )
            throw new ArgumentException("Transaction date filters must be UTC.");
        if (request.PostedFromUtc > request.PostedToUtc)
            throw new ArgumentException("PostedFromUtc cannot be later than PostedToUtc.");
        string? currency = string.IsNullOrWhiteSpace(request.Currency)
            ? null
            : request.Currency.Trim().ToUpperInvariant();
        if (currency is not null && currency != "VND")
            throw new ArgumentException("Currency filter must be VND.");
        return store.SearchAsync(
            new(
                request.AccountId,
                currency,
                request.Type,
                request.Status,
                request.PostedFromUtc,
                request.PostedToUtc,
                Math.Clamp(request.Page, 1, 1_000_000),
                Math.Clamp(request.PageSize, 1, 100)
            ),
            cancellationToken
        );
    }
}
