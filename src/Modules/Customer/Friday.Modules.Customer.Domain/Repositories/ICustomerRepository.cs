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
