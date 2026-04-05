using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;

namespace Friday.Modules.Admin.Domain.Repositories;

public interface IUserSessionRepository
{
    Task AddAsync(UserSession session, CancellationToken cancellationToken = default);
    Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserSession?> GetActiveByRefreshTokenHashAsync(
        string refreshTokenHashHex,
        CancellationToken cancellationToken = default
    );

    Task<UserSession?> GetByRefreshTokenHashAsync(
        string refreshTokenHashHex,
        CancellationToken cancellationToken = default
    );

    Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default);
}
