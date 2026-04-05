using Friday.Modules.Admin.Domain.Aggregates.RightAggregate;

namespace Friday.Modules.Admin.Domain.Repositories;

public interface IRightRepository
{
    Task AddAsync(Right right, CancellationToken cancellationToken = default);
    Task<Right?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Right>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Right>> GetByIdsAsync(
        int[] ids,
        CancellationToken cancellationToken = default
    );
}
