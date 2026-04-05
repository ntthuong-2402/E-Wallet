using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Friday.Modules.Admin.Infrastructure.Repositories;

public sealed class UserSessionRepository(FridayDbContext dbContext) : IUserSessionRepository
{
    public async Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<UserSession>().AddAsync(session, cancellationToken);
    }

    public Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<UserSession>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<UserSession?> GetActiveByRefreshTokenHashAsync(
        string refreshTokenHashHex,
        CancellationToken cancellationToken = default
    )
    {
        DateTime now = DateTime.UtcNow;
        return dbContext
            .Set<UserSession>()
            .FirstOrDefaultAsync(
                x =>
                    x.RefreshTokenHash == refreshTokenHashHex
                    && x.RevokedAtUtc == null
                    && x.ExpiresAtUtc > now,
                cancellationToken
            );
    }

    public Task<UserSession?> GetByRefreshTokenHashAsync(
        string refreshTokenHashHex,
        CancellationToken cancellationToken = default
    )
    {
        return dbContext
            .Set<UserSession>()
            .FirstOrDefaultAsync(x => x.RefreshTokenHash == refreshTokenHashHex, cancellationToken);
    }

    public async Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        List<UserSession> openSessions = await dbContext
            .Set<UserSession>()
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (UserSession session in openSessions)
        {
            session.Revoke();
        }
    }
}
