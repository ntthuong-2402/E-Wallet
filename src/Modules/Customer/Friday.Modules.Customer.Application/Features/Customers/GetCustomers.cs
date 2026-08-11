using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Customer.Application.Customers;
using Friday.Modules.Customer.Application.Errors;
using Friday.Modules.Customer.Application.Models;
using Friday.Modules.Customer.Domain.Customers;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Customer.Application.Features.Customers;

public sealed record GetCustomerByIdQuery(int CustomerId) : IQuery<CustomerDetailDto>;

public sealed class GetCustomerByIdHandler(ICustomerReadStore store)
    : IQueryHandler<GetCustomerByIdQuery, CustomerDetailDto>
{
    public async Task<CustomerDetailDto> HandleAsync(GetCustomerByIdQuery request, CancellationToken cancellationToken) =>
        await store.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new FridayException(CustomerErrorCodes.NotFound, "Customer was not found.", 404);
}

public sealed record GetCustomerByCodeQuery(string CustomerCode) : IQuery<CustomerDetailDto>;

public sealed class GetCustomerByCodeHandler(ICustomerReadStore store)
    : IQueryHandler<GetCustomerByCodeQuery, CustomerDetailDto>
{
    public async Task<CustomerDetailDto> HandleAsync(GetCustomerByCodeQuery request, CancellationToken cancellationToken) =>
        await store.GetByCodeAsync(request.CustomerCode, cancellationToken)
            ?? throw new FridayException(CustomerErrorCodes.NotFound, "Customer was not found.", 404);
}

public sealed record SearchCustomersQuery(
    string? CustomerCode = null,
    CustomerStatus? Status = null,
    DateTime? OpenedFrom = null,
    DateTime? OpenedTo = null,
    int Page = 1,
    int PageSize = 50,
    string SortBy = "openedOnUtc",
    string SortDirection = "desc"
) : IQuery<CustomerSearchPage>;

public sealed class SearchCustomersHandler(ICustomerReadStore store)
    : IQueryHandler<SearchCustomersQuery, CustomerSearchPage>
{
    private static readonly HashSet<string> SortFields =
        new(StringComparer.OrdinalIgnoreCase) { "id", "customerCode", "openedOnUtc", "status" };

    public Task<CustomerSearchPage> HandleAsync(SearchCustomersQuery request, CancellationToken cancellationToken)
    {
        int page = Math.Clamp(request.Page, 1, 1_000_000);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        string sortBy = SortFields.Contains(request.SortBy) ? request.SortBy : "openedOnUtc";
        bool descending = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        DateTime? openedFrom = EnsureUtc(request.OpenedFrom, nameof(request.OpenedFrom));
        DateTime? openedTo = EnsureUtc(request.OpenedTo, nameof(request.OpenedTo));
        if (openedFrom > openedTo)
            throw new ArgumentException("OpenedFrom cannot be later than OpenedTo.");
        return store.SearchAsync(new CustomerSearchCriteria(
            request.CustomerCode,
            request.Status,
            openedFrom,
            openedTo,
            page,
            pageSize,
            sortBy,
            descending
        ), cancellationToken);
    }

    private static DateTime? EnsureUtc(DateTime? value, string name)
    {
        if (value.HasValue && value.Value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Customer date filters must be UTC.", name);
        return value;
    }
}

public sealed record GetCustomerAuditQuery(int CustomerId, int Skip = 0, int Take = 50)
    : IQuery<IReadOnlyList<CustomerAuditDto>>;

public sealed class GetCustomerAuditHandler(ICustomerReadStore store)
    : IQueryHandler<GetCustomerAuditQuery, IReadOnlyList<CustomerAuditDto>>
{
    public Task<IReadOnlyList<CustomerAuditDto>> HandleAsync(
        GetCustomerAuditQuery request,
        CancellationToken cancellationToken
    ) => GetAsync(request, cancellationToken);

    private async Task<IReadOnlyList<CustomerAuditDto>> GetAsync(
        GetCustomerAuditQuery request,
        CancellationToken cancellationToken
    )
    {
        if (await store.GetByIdAsync(request.CustomerId, cancellationToken) is null)
            throw new FridayException(CustomerErrorCodes.NotFound, "Customer was not found.", 404);
        return await store.GetAuditAsync(
            request.CustomerId,
            Math.Clamp(request.Skip, 0, 1_000_000),
            Math.Clamp(request.Take, 1, 100),
            cancellationToken
        );
    }
}
