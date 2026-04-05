using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;

namespace Friday.Modules.Admin.Domain.Repositories;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetByIdWithPasswordAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetByLoginWithPasswordAsync(
        string login,
        CancellationToken cancellationToken = default
    );
    Task<bool> ExistsByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default
    );
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUserCodeAsync(string userCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default);
}
