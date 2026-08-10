using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Application.Authorization;
using Friday.Modules.Admin.Domain.Aggregates.RightAggregate;
using Friday.Modules.Admin.Domain.Aggregates.RoleAggregate;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Microsoft.EntityFrameworkCore;

namespace Friday.Modules.Admin.Infrastructure.Authorization;

public sealed class EfUserPermissionReader(FridayDbContext dbContext) : IUserPermissionReader
{
    public Task<bool> HasPermissionAsync(
        int userId,
        string permission,
        CancellationToken cancellationToken = default
    )
    {
        string normalized = permission.Trim().ToUpperInvariant();

        return (
            from userRole in dbContext.Set<UserRole>()
            join role in dbContext.Set<Role>() on userRole.RoleId equals role.Id
            join roleRight in dbContext.Set<RoleRight>() on role.Id equals roleRight.RoleId
            join right in dbContext.Set<Right>() on roleRight.RightId equals right.Id
            where
                userRole.UserId == userId
                && role.IsActive
                && right.Code == normalized
            select right.Id
        ).AnyAsync(cancellationToken);
    }
}
