using Friday.Modules.Customer.Domain.Auditing;

namespace Friday.Modules.Customer.Domain.Repositories;

public interface ICustomerAuditRepository
{
    Task AddAsync(CustomerChangeAudit audit, CancellationToken cancellationToken = default);
}
