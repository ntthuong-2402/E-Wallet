using Friday.Modules.Customer.Domain.Customers;
using Friday.Modules.Customer.Domain.Repositories;
using Friday.Modules.Customer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Friday.Modules.Customer.Infrastructure.Repositories;

public sealed class CustomerCreateReferenceRepository(CustomerDbContext dbContext)
    : ICustomerCreateReferenceRepository
{
    public async Task AcquireAsync(string refId, CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsRelational()) return;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({refId}, 0))",
            cancellationToken
        );
    }

    public Task<CustomerCreateReference?> GetAsync(
        string refId,
        CancellationToken cancellationToken = default
    ) => dbContext.CustomerCreateReferences
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.RefId == refId, cancellationToken);

    public Task AddAsync(
        CustomerCreateReference reference,
        CancellationToken cancellationToken = default
    ) => dbContext.CustomerCreateReferences.AddAsync(reference, cancellationToken).AsTask();
}
