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

public sealed record CreateInternalTransferCommand(
    string RefId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency,
    string? Description
) : IPaymentLedgerCommand<TransactionDto>;

public sealed class CreateInternalTransferHandler(
    ILedgerAccountRepository accounts,
    IFinancialTransactionRepository transactions,
    IIdempotencyRepository references,
    IPaymentLedgerActor actor,
    TimeProvider clock,
    IFinancialAuditRepository? audits = null
) : ICommandHandler<CreateInternalTransferCommand, TransactionDto>
{
    public const string Operation = "CREATE_INTERNAL_TRANSFER";

    public async Task<TransactionDto> HandleAsync(
        CreateInternalTransferCommand request,
        CancellationToken cancellationToken
    )
    {
        if (request.SourceAccountId == request.DestinationAccountId)
            throw new FridayException(
                PaymentLedgerErrorCodes.InvalidTransfer,
                "Source and destination accounts must differ."
            );
        if (request.Amount <= 0 || decimal.Truncate(request.Amount) != request.Amount)
            throw new FridayException(
                PaymentLedgerErrorCodes.InvalidTransfer,
                "VND amount must be a positive whole number."
            );
        if (!string.Equals(request.Currency?.Trim(), "VND", StringComparison.OrdinalIgnoreCase))
            throw new FridayException(
                PaymentLedgerErrorCodes.InvalidTransfer,
                "Currency must be VND."
            );
        string refId = IdempotencyRecord.NormalizeRefId(request.RefId);
        string hash = IdempotencySupport.Hash(
            request.SourceAccountId,
            request.DestinationAccountId,
            request.Amount,
            "VND",
            request.Description?.Trim()
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
        (LedgerAccount? first, LedgerAccount? second) = await accounts.GetForUpdateAsync(
            request.SourceAccountId,
            request.DestinationAccountId,
            cancellationToken
        );
        LedgerAccount source = Resolve(first, second, request.SourceAccountId);
        LedgerAccount destination = Resolve(first, second, request.DestinationAccountId);
        FinancialTransaction transaction;
        try
        {
            transaction = FinancialTransaction.PostInternalTransfer(
                source,
                destination,
                request.Amount,
                "VND",
                request.Description,
                clock.GetUtcNow().UtcDateTime
            );
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("insufficient", StringComparison.OrdinalIgnoreCase))
        {
            throw new FridayException(PaymentLedgerErrorCodes.InsufficientFunds, ex.Message, 409);
        }
        catch (InvalidOperationException ex)
        {
            throw new FridayException(PaymentLedgerErrorCodes.AccountInactive, ex.Message, 409);
        }
        catch (ArgumentException ex)
        {
            throw new FridayException(PaymentLedgerErrorCodes.InvalidTransfer, ex.Message);
        }
        await transactions.AddAsync(transaction, cancellationToken);
        TransactionDto response = TransactionDto.From(transaction);
        DateTime now = transaction.PostedOnUtc;
        if (audits is not null)
            await audits.AddAsync(
                FinancialAuditRecord.Success(
                    actor.UserId,
                    Operation,
                    refId,
                    null,
                    transaction.Id,
                    actor.TraceId,
                    now
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
                now
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
