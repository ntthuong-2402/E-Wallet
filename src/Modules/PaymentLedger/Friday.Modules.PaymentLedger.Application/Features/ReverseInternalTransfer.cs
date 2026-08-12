using System.Text.Json;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.PaymentLedger.Application.Actors;
using Friday.Modules.PaymentLedger.Application.Errors;
using Friday.Modules.PaymentLedger.Application.Models;
using Friday.Modules.PaymentLedger.Application.Persistence;
using Friday.Modules.PaymentLedger.Domain.Ledger;
using Friday.Modules.PaymentLedger.Domain.Repositories;
using LinKit.Core.Cqrs;

namespace Friday.Modules.PaymentLedger.Application.Features;

public sealed record ReverseInternalTransferCommand(
    Guid OriginalTransactionId,
    string RefId,
    string Reason
) : IPaymentLedgerCommand<TransactionDto>;

public sealed class ReverseInternalTransferHandler(
    ILedgerAccountRepository accounts,
    IFinancialTransactionRepository transactions,
    IIdempotencyRepository references,
    IPaymentLedgerActor actor,
    TimeProvider clock,
    IFinancialAuditRepository? audits = null
) : ICommandHandler<ReverseInternalTransferCommand, TransactionDto>
{
    public const string Operation = "REVERSE_INTERNAL_TRANSFER";

    public async Task<TransactionDto> HandleAsync(
        ReverseInternalTransferCommand request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new FridayException(
                PaymentLedgerErrorCodes.InvalidReversal,
                "Reversal reason is required."
            );
        string refId = IdempotencyRecord.NormalizeRefId(request.RefId);
        string hash = IdempotencySupport.Hash(
            request.OriginalTransactionId,
            request.Reason?.Trim()
        );
        await references.AcquireAsync(actor.UserId, Operation, refId, cancellationToken);
        IdempotencyRecord? existing = await references.GetAsync(
            actor.UserId,
            Operation,
            refId,
            cancellationToken
        );
        if (existing is not null)
            return IdempotencySupport.Replay<TransactionDto>(existing, hash);
        FinancialTransaction original =
            await transactions.GetAsync(request.OriginalTransactionId, cancellationToken)
            ?? throw new FridayException(
                PaymentLedgerErrorCodes.TransactionNotFound,
                "Transaction was not found.",
                404
            );
        (LedgerAccount? first, LedgerAccount? second) = await accounts.GetForUpdateAsync(
            original.SourceAccountId,
            original.DestinationAccountId,
            cancellationToken
        );
        LedgerAccount source = Resolve(first, second, original.SourceAccountId);
        LedgerAccount destination = Resolve(first, second, original.DestinationAccountId);
        FinancialTransaction reversal;
        try
        {
            reversal = original.Reverse(
                source,
                destination,
                request.Reason!,
                clock.GetUtcNow().UtcDateTime
            );
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("insufficient", StringComparison.OrdinalIgnoreCase))
        {
            throw new FridayException(
                PaymentLedgerErrorCodes.InsufficientFunds,
                "Beneficiary has insufficient funds for a full reversal.",
                409
            );
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw new FridayException(PaymentLedgerErrorCodes.InvalidReversal, ex.Message, 409);
        }
        await transactions.AddAsync(reversal, cancellationToken);
        TransactionDto response = TransactionDto.From(reversal);
        if (audits is not null)
            await audits.AddAsync(
                FinancialAuditRecord.Success(
                    actor.UserId,
                    Operation,
                    refId,
                    null,
                    reversal.Id,
                    actor.TraceId,
                    reversal.PostedOnUtc
                ),
                cancellationToken
            );
        await references.AddAsync(
            IdempotencyRecord.Create(
                actor.UserId,
                Operation,
                refId,
                hash,
                JsonSerializer.Serialize(response, IdempotencySupport.JsonOptions),
                reversal.PostedOnUtc
            ),
            cancellationToken
        );
        return response;
    }

    private static LedgerAccount Resolve(LedgerAccount? first, LedgerAccount? second, Guid id) =>
        first?.Id == id ? first
        : second?.Id == id ? second
        : throw new FridayException(
            PaymentLedgerErrorCodes.AccountNotFound,
            "Ledger account was not found.",
            404
        );
}
