using Friday.Modules.Customer.Domain.Customers;
using Friday.Modules.Customer.Domain.Repositories;
using Friday.Modules.Customer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CustomerAggregate = Friday.Modules.Customer.Domain.Customers.Customer;

namespace Friday.Modules.Customer.Infrastructure.Repositories;

public sealed class CustomerRepository(CustomerDbContext dbContext) : ICustomerRepository
{
    public Task<CustomerAggregate?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Customers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<CustomerAggregate?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        string normalized = customerCode.Trim().ToUpperInvariant();
        return dbContext.Customers.SingleOrDefaultAsync(x => x.CustomerCode == normalized, cancellationToken);
    }

    public Task<bool> CustomerCodeExistsAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        string normalized = customerCode.Trim().ToUpperInvariant();
        return dbContext.Customers.AnyAsync(x => x.CustomerCode == normalized, cancellationToken);
    }

    public Task<bool> CitizenDocumentExistsAsync(
        CitizenDocumentType documentType,
        string issuingCountryCode,
        string documentNumber,
        int? excludingCustomerId = null,
        CancellationToken cancellationToken = default
    ) => dbContext.Customers.AnyAsync(
        x => x.CitizenDocumentType == documentType
            && x.CitizenIssuingCountryCode == issuingCountryCode
            && x.CitizenDocumentNumber == documentNumber
            && (!excludingCustomerId.HasValue || x.Id != excludingCustomerId.Value),
        cancellationToken
    );

    public Task AddAsync(CustomerAggregate customer, CancellationToken cancellationToken = default) =>
        dbContext.Customers.AddAsync(customer, cancellationToken).AsTask();
}
