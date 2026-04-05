using Friday.Modules.Admin.Domain.Aggregates.RoleAggregate;

namespace Friday.Modules.Admin.Domain.Repositories;

public interface IRoleRepository
{
    Task AddAsync(Role role, CancellationToken cancellationToken = default);
    Task<Role?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> GetByIdsAsync(
        int[] ids,
        CancellationToken cancellationToken = default
    );
}
