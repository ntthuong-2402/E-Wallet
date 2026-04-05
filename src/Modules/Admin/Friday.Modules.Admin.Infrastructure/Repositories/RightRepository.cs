using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Domain.Aggregates.RightAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Friday.Modules.Admin.Infrastructure.Repositories;

public sealed class RightRepository(FridayDbContext dbContext) : IRightRepository
{
    public async Task AddAsync(Right right, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<Right>().AddAsync(right, cancellationToken);
    }

    public Task<Right?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<Right>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        string normalized = code.Trim().ToUpperInvariant();
        return dbContext
            .Set<Right>()
            .AnyAsync(x => x.Code.ToUpper() == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Right>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Right>().OrderBy(x => x.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Right>> GetByIdsAsync(
        int[] ids,
        CancellationToken cancellationToken = default
    )
    {
        return await dbContext
            .Set<Right>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }
}
