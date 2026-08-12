using Friday.Modules.Customer.Application.Customers;
using Friday.Modules.Customer.Application.Models;
using Friday.Modules.Customer.Domain.Auditing;
using Friday.Modules.Customer.Domain.Customers;
using Friday.Modules.Customer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CustomerAggregate = Friday.Modules.Customer.Domain.Customers.Customer;

namespace Friday.Modules.Customer.Infrastructure.Repositories;

public sealed class CustomerReadStore(CustomerDbContext dbContext) : ICustomerReadStore
{
    public Task<CustomerDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        ProjectDetails(dbContext.Customers.AsNoTracking().Where(x => x.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<CustomerDetailDto?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        string normalized = customerCode.Trim().ToUpperInvariant();
        return ProjectDetails(dbContext.Customers.AsNoTracking().Where(x => x.CustomerCode == normalized))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<CustomerDetailDto?> GetByAccountIdAsync(
        string accountId,
        CancellationToken cancellationToken = default
    ) => ProjectDetails(
        from customer in dbContext.Customers.AsNoTracking()
        join linkage in dbContext.CustomerAccountLinkages.AsNoTracking()
            on customer.Id equals linkage.CustomerId
        where linkage.AccountId == accountId && linkage.UnlinkedOnUtc == null
        select customer
    ).SingleOrDefaultAsync(cancellationToken);

    public async Task<CustomerSearchPage> SearchAsync(
        CustomerSearchCriteria criteria,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<CustomerAggregate> query = dbContext.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(criteria.CustomerCode))
        {
            string code = criteria.CustomerCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.CustomerCode == code);
        }
        if (criteria.Status.HasValue) query = query.Where(x => x.Status == criteria.Status.Value);
        if (criteria.OpenedFrom.HasValue) query = query.Where(x => x.OpenedOnUtc >= criteria.OpenedFrom.Value);
        if (criteria.OpenedTo.HasValue) query = query.Where(x => x.OpenedOnUtc <= criteria.OpenedTo.Value);

        int total = await query.CountAsync(cancellationToken);
        query = Order(query, criteria.SortBy, criteria.Descending);
        CustomerListItemDto[] items = await query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(x => new CustomerListItemDto(
                x.Id,
                x.CustomerCode,
                x.FullName.Substring(0, 1) + "***",
                x.CitizenDocumentType,
                x.CitizenIdMasked,
                x.Status,
                x.OpenedOnUtc,
                x.Version
            ))
            .ToArrayAsync(cancellationToken);
        return new CustomerSearchPage(items, total, criteria.Page, criteria.PageSize);
    }

    public async Task<IReadOnlyList<CustomerAuditDto>> GetAuditAsync(
        int customerId,
        int skip,
        int take,
        CancellationToken cancellationToken = default
    )
    {
        return await dbContext.CustomerChangeAudits
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.OccurredOnUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(take)
            .Select(x => new CustomerAuditDto(
                x.Id,
                x.EventType,
                x.ChangedFieldsJson,
                x.FromStatus,
                x.ToStatus,
                x.Reason,
                x.Outcome,
                x.ActorUserId,
                x.TraceId,
                x.OccurredOnUtc
            ))
            .ToArrayAsync(cancellationToken);
    }

    private static IQueryable<CustomerDetailDto> ProjectDetails(IQueryable<CustomerAggregate> query) =>
        query.Select(x => new CustomerDetailDto(
            x.Id,
            x.CustomerCode,
            x.FullName.Substring(0, 1) + "***",
            x.CitizenDocumentType,
            x.CitizenIssuingCountryCode,
            x.CitizenIdMasked,
            x.Status,
            x.OpenedOnUtc,
            x.Version,
            x.CreatedOnUtc,
            x.UpdatedOnUtc
        ));

    private static IQueryable<CustomerAggregate> Order(IQueryable<CustomerAggregate> query, string sortBy, bool descending) =>
        (sortBy.ToLowerInvariant(), descending) switch
        {
            ("id", false) => query.OrderBy(x => x.Id),
            ("id", true) => query.OrderByDescending(x => x.Id),
            ("customercode", false) => query.OrderBy(x => x.CustomerCode).ThenBy(x => x.Id),
            ("customercode", true) => query.OrderByDescending(x => x.CustomerCode).ThenByDescending(x => x.Id),
            ("status", false) => query.OrderBy(x => x.Status).ThenBy(x => x.Id),
            ("status", true) => query.OrderByDescending(x => x.Status).ThenByDescending(x => x.Id),
            (_, false) => query.OrderBy(x => x.OpenedOnUtc).ThenBy(x => x.Id),
            _ => query.OrderByDescending(x => x.OpenedOnUtc).ThenByDescending(x => x.Id),
        };
}
