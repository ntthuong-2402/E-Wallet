using Friday.API.Common;
using Friday.Modules.Customer.Application.Authorization;
using Friday.Modules.Customer.Application.Features.Customers;
using Friday.Modules.Customer.Application.Models;
using Friday.Modules.Customer.Domain.Customers;
using LinKit.Core.Cqrs;

namespace Friday.API.Modules.Customer;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerModule(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/customers")
            .WithTags("Customers")
            .RequireAuthorization();

        group.MapPost("/", async (
            CreateCustomerRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            CustomerDetailDto result = await mediator.SendAsync(
                new CreateCustomerCommand(
                    request.FullName,
                    request.DateOfBirth,
                    request.DocumentType,
                    request.IssuingCountryCode,
                    request.CitizenDocumentNumber
                ),
                cancellationToken
            );
            return ApiResults.Ok(context, result, "Customer created.");
        }).RequireAuthorization(CustomerPermissions.Create).RequireRateLimiting("customer-write");

        group.MapGet("/{id:int}", async (
            int id,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ApiResults.Ok(context, await mediator.QueryAsync(
                new GetCustomerByIdQuery(id), cancellationToken
            ))
        ).RequireAuthorization(CustomerPermissions.Read).RequireRateLimiting("customer-read");

        group.MapGet("/by-code/{customerCode}", async (
            string customerCode,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ApiResults.Ok(context, await mediator.QueryAsync(
                new GetCustomerByCodeQuery(customerCode), cancellationToken
            ))
        ).RequireAuthorization(CustomerPermissions.Read).RequireRateLimiting("customer-read");

        group.MapGet("/", async (
            string? customerCode,
            CustomerStatus? status,
            DateTime? openedFrom,
            DateTime? openedTo,
            int page,
            int pageSize,
            string? sortBy,
            string? sortDirection,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            CustomerSearchPage result = await mediator.QueryAsync(
                new SearchCustomersQuery(
                    customerCode,
                    status,
                    openedFrom,
                    openedTo,
                    page <= 0 ? 1 : page,
                    pageSize <= 0 ? 50 : pageSize,
                    sortBy ?? "openedOnUtc",
                    sortDirection ?? "desc"
                ),
                cancellationToken
            );
            context.Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
            context.Response.Headers["X-Page"] = result.Page.ToString();
            context.Response.Headers["X-Page-Size"] = result.PageSize.ToString();
            return ApiResults.Ok(context, result.Items);
        }).RequireAuthorization(CustomerPermissions.Read).RequireRateLimiting("customer-read");

        group.MapPut("/{id:int}", async (
            int id,
            UpdateCustomerProfileRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ApiResults.Ok(context, await mediator.SendAsync(
                new UpdateCustomerProfileCommand(
                    id,
                    request.ExpectedVersion,
                    request.FullName,
                    request.DateOfBirth,
                    request.DocumentType,
                    request.IssuingCountryCode,
                    request.CitizenDocumentNumber
                ),
                cancellationToken
            ))
        ).RequireAuthorization(CustomerPermissions.Update).RequireRateLimiting("customer-write");

        group.MapPatch("/{id:int}/status", async (
            int id,
            ChangeCustomerStatusRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse(request.TargetStatus, true, out CustomerStatus targetStatus))
                throw new ArgumentException("Unsupported Customer status.", nameof(request.TargetStatus));
            return ApiResults.Ok(context, await mediator.SendAsync(
                new ChangeCustomerStatusCommand(id, request.ExpectedVersion, targetStatus, request.Reason),
                cancellationToken
            ));
        }).RequireAuthorization(CustomerPermissions.StatusChange).RequireRateLimiting("customer-write");

        group.MapGet("/{id:int}/audit", async (
            int id,
            int skip,
            int take,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken) =>
            ApiResults.Ok(context, await mediator.QueryAsync(
                new GetCustomerAuditQuery(id, skip, take <= 0 ? 50 : take), cancellationToken
            ))
        ).RequireAuthorization(CustomerPermissions.AuditRead).RequireRateLimiting("customer-read");

        return endpoints;
    }
}

public sealed record CreateCustomerRequest(
    string FullName,
    DateOnly? DateOfBirth,
    CitizenDocumentType DocumentType,
    string IssuingCountryCode,
    string CitizenDocumentNumber
);

public sealed record UpdateCustomerProfileRequest(
    long ExpectedVersion,
    string FullName,
    DateOnly? DateOfBirth,
    CitizenDocumentType? DocumentType,
    string? IssuingCountryCode,
    string? CitizenDocumentNumber
);

public sealed record ChangeCustomerStatusRequest(long ExpectedVersion, string TargetStatus, string Reason);
