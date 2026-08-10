using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.Modules.Admin.Application.Authorization;
using Friday.Modules.Admin.Application.Configuration;
using Friday.Modules.Admin.Domain.Aggregates.RightAggregate;
using Friday.Modules.Admin.Domain.Aggregates.RoleAggregate;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Friday.Modules.Admin.Infrastructure.Bootstrap;

public sealed class AdminSecurityBootstrapper(
    FridayDbContext dbContext,
    IOptions<AdminBootstrapOptions> options,
    ILogger<AdminSecurityBootstrapper> logger
)
{
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        List<Right> rights = await dbContext.Set<Right>().ToListAsync(cancellationToken);
        foreach (string code in AdminPermissions.All)
        {
            if (rights.All(x => !string.Equals(x.Code, code, StringComparison.Ordinal)))
            {
                Right right = Right.Create(code, code.Replace('_', ' '));
                rights.Add(right);
                await dbContext.Set<Right>().AddAsync(right, cancellationToken);
            }
        }

        Role? superAdmin = await dbContext
            .Set<Role>()
            .Include(x => x.RoleRights)
            .FirstOrDefaultAsync(x => x.Code == "SUPER_ADMIN", cancellationToken);
        if (superAdmin is null)
        {
            superAdmin = Role.Create("SUPER_ADMIN", "Super Administrator");
            await dbContext.Set<Role>().AddAsync(superAdmin, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        int[] desiredRightIds = rights.Select(x => x.Id).OrderBy(x => x).ToArray();
        int[] currentRightIds = superAdmin.RoleRights.Select(x => x.RightId).OrderBy(x => x).ToArray();
        if (!currentRightIds.SequenceEqual(desiredRightIds))
        {
            superAdmin.SetRights(desiredRightIds);
        }

        string userCode = options.Value.SuperAdminUserCode.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(userCode))
        {
            logger.LogWarning(
                "Admin bootstrap created SUPER_ADMIN permissions but no user was assigned because Admin:Bootstrap:SuperAdminUserCode is empty."
            );
        }
        else
        {
            User? user = await dbContext
                .Set<User>()
                .Include(x => x.UserRoles)
                .FirstOrDefaultAsync(x => x.UserCode == userCode, cancellationToken);
            if (user is null)
            {
                throw new InvalidOperationException(
                    $"Bootstrap super-admin user code '{userCode}' was not found."
                );
            }

            user.AssignRole(superAdmin.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
