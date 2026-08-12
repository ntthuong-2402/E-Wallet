using Friday.Modules.Customer.Domain.Customers;
using Friday.Modules.Customer.Domain.Repositories;
using Friday.Modules.Customer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Friday.Modules.Customer.Infrastructure.Repositories;

public sealed class CustomerAccountLinkageRepository(CustomerDbContext dbContext)
    : ICustomerAccountLinkageRepository
{
    public Task<CustomerAccountLinkage?> GetActiveByCustomerIdAsync(
        int customerId,
        CancellationToken cancellationToken = default
    ) => dbContext.CustomerAccountLinkages.SingleOrDefaultAsync(
        x => x.CustomerId == customerId && x.UnlinkedOnUtc == null,
        cancellationToken
    );

    public Task<CustomerAccountLinkage?> GetActiveByAccountIdAsync(
        string accountId,
        CancellationToken cancellationToken = default
    ) => dbContext.CustomerAccountLinkages.SingleOrDefaultAsync(
        x => x.AccountId == accountId && x.UnlinkedOnUtc == null,
        cancellationToken
    );

    public Task AddAsync(
        CustomerAccountLinkage linkage,
        CancellationToken cancellationToken = default
    ) => dbContext.CustomerAccountLinkages.AddAsync(linkage, cancellationToken).AsTask();
}
