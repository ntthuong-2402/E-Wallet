using Friday.API.Common;
using Friday.Modules.PaymentLedger.Application.Authorization;
using Friday.Modules.PaymentLedger.Application.Features;
using Friday.Modules.PaymentLedger.Application.Models;
using Friday.Modules.PaymentLedger.Domain.Ledger;
using LinKit.Core.Cqrs;

namespace Friday.API.Modules.PaymentLedger;

public static class PaymentLedgerEndpoints
{
    public static IEndpointRouteBuilder MapPaymentLedgerModule(
        this IEndpointRouteBuilder endpoints
    )
    {
        RouteGroupBuilder accounts = endpoints.MapGroup("/api/ledger/accounts")
            .WithTags("Ledger Accounts")
            .RequireAuthorization();

        accounts.MapPost("/", async (
            OpenLedgerAccountRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ApiResults.Ok(
                context,
                await mediator.SendAsync(
                    new OpenLedgerAccountCommand(request.RefId, request.AccountRef),
                    cancellationToken
                ),
                "Ledger account opened."
            )
        ).RequireAuthorization(PaymentLedgerPermissions.LedgerAccountsCreate)
            .RequireRateLimiting("transaction-write");

        accounts.MapGet("/{accountId:guid}", async (
            Guid accountId,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ApiResults.Ok(
                context,
                await mediator.QueryAsync(new GetLedgerAccountQuery(accountId), cancellationToken)
            )
        ).RequireAuthorization(PaymentLedgerPermissions.LedgerAccountsRead)
            .RequireRateLimiting("transaction-read");

        RouteGroupBuilder transactions = endpoints.MapGroup("/api/transactions")
            .WithTags("Transactions")
            .RequireAuthorization();

        transactions.MapPost("/transfers", async (
            CreateInternalTransferRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ApiResults.Ok(
                context,
                await mediator.SendAsync(
                    new CreateInternalTransferCommand(
                        request.RefId,
                        request.SourceAccountId,
                        request.DestinationAccountId,
                        request.Amount,
                        request.Currency,
                        request.Description
                    ),
                    cancellationToken
                ),
                "Internal transfer posted."
            )
        ).RequireAuthorization(PaymentLedgerPermissions.TransactionsTransferCreate)
            .RequireRateLimiting("transaction-write");

        transactions.MapPost("/{transactionId:guid}/reversals", async (
            Guid transactionId,
            ReverseInternalTransferRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ApiResults.Ok(
                context,
                await mediator.SendAsync(
                    new ReverseInternalTransferCommand(transactionId, request.RefId, request.Reason),
                    cancellationToken
                ),
                "Internal transfer reversed."
            )
        ).RequireAuthorization(PaymentLedgerPermissions.TransactionsReverse)
            .RequireRateLimiting("transaction-write");

        transactions.MapGet("/{transactionId:guid}", async (
            Guid transactionId,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ApiResults.Ok(
                context,
                await mediator.QueryAsync(
                    new GetTransactionByIdQuery(transactionId),
                    cancellationToken
                )
            )
        ).RequireAuthorization(PaymentLedgerPermissions.TransactionsRead)
            .RequireRateLimiting("transaction-read");

        transactions.MapGet("/by-ref/{refId}", async (
            string refId,
            string? operation,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ApiResults.Ok(
                context,
                await mediator.QueryAsync(
                    new GetTransactionByReferenceQuery(
                        operation ?? CreateInternalTransferHandler.Operation,
                        refId
                    ),
                    cancellationToken
                )
            )
        ).RequireAuthorization(PaymentLedgerPermissions.TransactionsRead)
            .RequireRateLimiting("transaction-read");

        transactions.MapGet("/", async (
            Guid? accountId,
            string? currency,
            FinancialTransactionType? type,
            FinancialTransactionStatus? status,
            DateTime? postedFromUtc,
            DateTime? postedToUtc,
            int page,
            int pageSize,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            TransactionSearchPage result = await mediator.QueryAsync(
                new SearchTransactionsQuery(
                    accountId,
                    currency,
                    type,
                    status,
                    postedFromUtc,
                    postedToUtc,
                    page <= 0 ? 1 : page,
                    pageSize <= 0 ? 50 : pageSize
                ),
                cancellationToken
            );
            context.Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
            context.Response.Headers["X-Page"] = result.Page.ToString();
            context.Response.Headers["X-Page-Size"] = result.PageSize.ToString();
            return ApiResults.Ok(context, result.Items);
        }).RequireAuthorization(PaymentLedgerPermissions.TransactionsRead)
            .RequireRateLimiting("transaction-read");

        return endpoints;
    }
}

public sealed record OpenLedgerAccountRequest(string RefId, string AccountRef);

public sealed record CreateInternalTransferRequest(
    string RefId,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency,
    string? Description
);

public sealed record ReverseInternalTransferRequest(string RefId, string Reason);
