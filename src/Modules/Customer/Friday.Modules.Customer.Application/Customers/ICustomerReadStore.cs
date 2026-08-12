using Friday.Modules.Customer.Application.Models;

namespace Friday.Modules.Customer.Application.Customers;

public interface ICustomerReadStore
{
    Task<CustomerDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CustomerDetailDto?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default);
    Task<CustomerDetailDto?> GetByAccountIdAsync(string accountId, CancellationToken cancellationToken = default);
    Task<CustomerSearchPage> SearchAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerAuditDto>> GetAuditAsync(
        int customerId,
        int skip,
        int take,
        CancellationToken cancellationToken = default
    );
}

public interface IExternalAccountDirectory
{
    Task<bool> ExistsAsync(string accountId, CancellationToken cancellationToken = default);
}
