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
        int? superAdminRoleId = await (
            from userRole in dbContext.Set<UserRole>()
            join role in dbContext.Set<Role>() on userRole.RoleId equals role.Id
            where userRole.UserId == userId && role.Code == "SUPER_ADMIN" && role.IsActive
            select (int?)role.Id
        ).FirstOrDefaultAsync(cancellationToken);

        if (superAdminRoleId is null)
        {
            return;
        }

        await LockRoleAsync(superAdminRoleId.Value, cancellationToken);

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

    public async Task EnsureCanRemoveRoleAsync(
        int userId,
        int roleId,
        CancellationToken cancellationToken = default
    )
    {
        Role? role = await dbContext
            .Set<Role>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken);
        if (role is null || !string.Equals(role.Code, "SUPER_ADMIN", StringComparison.Ordinal))
        {
            return;
        }

        await LockRoleAsync(roleId, cancellationToken);

        bool targetHasRole = await dbContext
            .Set<UserRole>()
            .AnyAsync(x => x.UserId == userId && x.RoleId == roleId, cancellationToken);
        if (!targetHasRole)
        {
            return;
        }

        int assignedSuperAdmins = await dbContext
            .Set<UserRole>()
            .CountAsync(x => x.RoleId == roleId, cancellationToken);
        if (assignedSuperAdmins <= 1)
        {
            throw new FridayException(
                ErrorCodes.Admin.LastAdministratorProtection,
                "The SUPER_ADMIN role cannot be removed from the last assigned administrator.",
                StatusCodes.Status409Conflict
            );
        }
    }

    private async Task LockRoleAsync(int roleId, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        await dbContext
            .Set<Role>()
            .FromSqlInterpolated(
                $"""
                SELECT "Id", "Code", "Name", "IsActive", "CreatedOnUtc", "UpdatedOnUtc"
                FROM admin.roles
                WHERE "Id" = {roleId}
                FOR UPDATE
                """
            )
            .SingleAsync(cancellationToken);
    }
}
