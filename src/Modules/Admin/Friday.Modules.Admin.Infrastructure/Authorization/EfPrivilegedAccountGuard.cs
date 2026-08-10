using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Application.Authorization;
using Friday.Modules.Admin.Domain.Aggregates.RoleAggregate;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Friday.Modules.Admin.Infrastructure.Authorization;

public sealed class EfPrivilegedAccountGuard(FridayDbContext dbContext) : IPrivilegedAccountGuard
{
    public async Task EnsureCanDisableAsync(int userId, CancellationToken cancellationToken = default)
    {
        bool targetIsSuperAdmin = await (
            from userRole in dbContext.Set<UserRole>()
            join role in dbContext.Set<Role>() on userRole.RoleId equals role.Id
            where userRole.UserId == userId && role.Code == "SUPER_ADMIN" && role.IsActive
            select userRole.UserId
        ).AnyAsync(cancellationToken);

        if (!targetIsSuperAdmin)
        {
            return;
        }

        int enabledSuperAdmins = await (
            from userRole in dbContext.Set<UserRole>()
            join role in dbContext.Set<Role>() on userRole.RoleId equals role.Id
            join user in dbContext.Set<User>() on userRole.UserId equals user.Id
            where role.Code == "SUPER_ADMIN" && role.IsActive && user.IsActive && !user.IsLocked
            select user.Id
        ).Distinct().CountAsync(cancellationToken);

        if (enabledSuperAdmins <= 1)
        {
            throw new FridayException(
                ErrorCodes.Admin.LastAdministratorProtection,
                "The last enabled super administrator cannot be disabled.",
                StatusCodes.Status409Conflict
            );
        }
    }
}
