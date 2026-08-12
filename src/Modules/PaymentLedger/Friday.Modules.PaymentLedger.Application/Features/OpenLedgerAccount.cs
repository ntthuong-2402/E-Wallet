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

public sealed record OpenLedgerAccountCommand(string RefId, string AccountRef)
    : IPaymentLedgerCommand<LedgerAccountDto>;

public sealed class OpenLedgerAccountHandler(
    ILedgerAccountRepository accounts,
    IIdempotencyRepository references,
    IPaymentLedgerActor actor,
    TimeProvider clock,
    IFinancialAuditRepository? audits = null
) : ICommandHandler<OpenLedgerAccountCommand, LedgerAccountDto>
{
    public const string Operation = "OPEN_LEDGER_ACCOUNT";

    public async Task<LedgerAccountDto> HandleAsync(
        OpenLedgerAccountCommand request,
        CancellationToken cancellationToken
    )
    {
        string refId = IdempotencyRecord.NormalizeRefId(request.RefId);
        string accountRef = request.AccountRef?.Trim() ?? string.Empty;
        string hash = IdempotencySupport.Hash(accountRef);
        await references.AcquireAsync(actor.UserId, Operation, refId, cancellationToken);
        IdempotencyRecord? existing = await references.GetAsync(
            actor.UserId,
            Operation,
            refId,
            cancellationToken
        );
        if (existing is not null)
            return IdempotencySupport.Replay<LedgerAccountDto>(existing, hash);
        if (await accounts.GetByRefAsync(accountRef, cancellationToken) is not null)
            throw new FridayException(
                PaymentLedgerErrorCodes.AccountRefConflict,
                "Ledger AccountRef already exists.",
                409
            );
        DateTime now = clock.GetUtcNow().UtcDateTime;
        LedgerAccount account = LedgerAccount.Open(accountRef, now);
        await accounts.AddAsync(account, cancellationToken);
        LedgerAccountDto response = LedgerAccountDto.From(account);
        if (audits is not null)
            await audits.AddAsync(
                FinancialAuditRecord.Success(
                    actor.UserId,
                    Operation,
                    refId,
                    account.Id,
                    null,
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
}
