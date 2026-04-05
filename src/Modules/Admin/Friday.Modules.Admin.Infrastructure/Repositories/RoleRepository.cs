using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Domain.Aggregates.RoleAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Friday.Modules.Admin.Infrastructure.Repositories;

public sealed class RoleRepository(FridayDbContext dbContext) : IRoleRepository
{
    public async Task AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<Role>().AddAsync(role, cancellationToken);
    }

    public Task<Role?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return dbContext
            .Set<Role>()
            .Include(x => x.RoleRights)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        string normalized = code.Trim().ToUpperInvariant();
        return dbContext
            .Set<Role>()
            .AnyAsync(x => x.Code.ToUpper() == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext
            .Set<Role>()
            .Include(x => x.RoleRights)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> GetByIdsAsync(
        int[] ids,
        CancellationToken cancellationToken = default
    )
    {
        return await dbContext
            .Set<Role>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }
}
