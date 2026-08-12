using Friday.Modules.Customer.Domain.Customers;
using CustomerAggregate = Friday.Modules.Customer.Domain.Customers.Customer;

namespace Friday.Modules.Customer.Domain.Repositories;

public interface ICustomerRepository
{
    Task<CustomerAggregate?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CustomerAggregate?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default);
    Task<bool> CustomerCodeExistsAsync(string customerCode, CancellationToken cancellationToken = default);
    Task<bool> CitizenDocumentExistsAsync(
        CitizenDocumentType documentType,
        string issuingCountryCode,
        string documentNumber,
        int? excludingCustomerId = null,
        CancellationToken cancellationToken = default
    );
    Task AddAsync(CustomerAggregate customer, CancellationToken cancellationToken = default);
}

public interface ICustomerCreateReferenceRepository
{
    Task AcquireAsync(string refId, CancellationToken cancellationToken = default);
    Task<CustomerCreateReference?> GetAsync(string refId, CancellationToken cancellationToken = default);
    Task AddAsync(CustomerCreateReference reference, CancellationToken cancellationToken = default);
}

public interface ICustomerAccountLinkageRepository
{
    Task<CustomerAccountLinkage?> GetActiveByCustomerIdAsync(
        int customerId,
        CancellationToken cancellationToken = default
    );
    Task<CustomerAccountLinkage?> GetActiveByAccountIdAsync(
        string accountId,
        CancellationToken cancellationToken = default
    );
    Task AddAsync(CustomerAccountLinkage linkage, CancellationToken cancellationToken = default);
}
