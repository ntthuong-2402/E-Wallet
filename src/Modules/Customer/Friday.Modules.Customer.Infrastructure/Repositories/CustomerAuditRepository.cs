using Friday.Modules.Customer.Domain.Auditing;
using Friday.Modules.Customer.Domain.Repositories;
using Friday.Modules.Customer.Infrastructure.Persistence;

namespace Friday.Modules.Customer.Infrastructure.Repositories;

public sealed class CustomerAuditRepository(CustomerDbContext dbContext) : ICustomerAuditRepository
{
    public Task AddAsync(CustomerChangeAudit audit, CancellationToken cancellationToken = default) =>
        dbContext.CustomerChangeAudits.AddAsync(audit, cancellationToken).AsTask();
}
