using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Friday.Modules.Admin.Infrastructure.Repositories;

public sealed class UserRepository(FridayDbContext dbContext) : IUserRepository
{
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<User>().AddAsync(user, cancellationToken);
    }

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return dbContext
            .Set<User>()
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<User?> GetByIdWithPasswordAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        return dbContext
            .Set<User>()
            .Include(x => x.UserRoles)
            .Include(x => x.PasswordCredential)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<User?> GetByLoginWithPasswordAsync(
        string login,
        CancellationToken cancellationToken = default
    )
    {
        string trimmed = login.Trim();
        string normalizedEmail = trimmed.ToLowerInvariant();
        string upper = trimmed.ToUpperInvariant();

        return dbContext
            .Set<User>()
            .Include(x => x.UserRoles)
            .Include(x => x.PasswordCredential)
            .FirstOrDefaultAsync(
                x =>
                    x.Username.ToUpper() == upper
                    || x.Email == normalizedEmail
                    || x.UserCode == upper,
                cancellationToken
            );
    }

    public Task<bool> ExistsByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default
    )
    {
        string normalized = username.Trim().ToUpperInvariant();
        return dbContext
            .Set<User>()
            .AnyAsync(x => x.Username.ToUpper() == normalized, cancellationToken);
    }

    public Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        string normalized = email.Trim().ToLowerInvariant();
        return dbContext.Set<User>().AnyAsync(x => x.Email == normalized, cancellationToken);
    }

    public Task<bool> ExistsByUserCodeAsync(
        string userCode,
        CancellationToken cancellationToken = default
    )
    {
        string normalized = userCode.Trim().ToUpperInvariant();
        return dbContext.Set<User>().AnyAsync(x => x.UserCode == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext
            .Set<User>()
            .Include(x => x.UserRoles)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
