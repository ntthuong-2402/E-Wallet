using Friday.BuildingBlocks.Infrastructure.Persistence;
using Friday.BuildingBlocks.Application.Authorization;
using Friday.Modules.Admin.Application.Authorization;
using Friday.Modules.Admin.Application.Configuration;
using Friday.Modules.Admin.Application.Security;
using Friday.Modules.Admin.Application.Auditing;
using Friday.Modules.Admin.Domain.Aggregates.RightAggregate;
using Friday.Modules.Admin.Domain.Aggregates.RoleAggregate;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Friday.Modules.Admin.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Friday.Modules.Admin.Infrastructure.Bootstrap;

public sealed class AdminSecurityBootstrapper(
    FridayDbContext dbContext,
    IOptions<AdminBootstrapOptions> options,
    IPasswordHasher<CredentialUser> passwordHasher,
    IPasswordPolicy passwordPolicy,
    ISecurityAuditWriter audit,
    ILogger<AdminSecurityBootstrapper> logger,
    IEnumerable<IPermissionContribution> permissionContributions
)
{
    private static readonly CredentialUser CredentialMarker = new();

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        List<Right> rights = await dbContext.Set<Right>().ToListAsync(cancellationToken);
        string[] permissionCodes = permissionContributions
            .SelectMany(x => x.Permissions)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        foreach (string code in permissionCodes)
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

        AdminBootstrapOptions bootstrap = options.Value;
        if (!bootstrap.Enabled)
        {
            logger.LogWarning(
                "Admin bootstrap created SUPER_ADMIN permissions but system-user provisioning is disabled."
            );
        }
        else
        {
            string userCode = RequireValue(bootstrap.UserCode, nameof(bootstrap.UserCode))
                .ToUpperInvariant();
            string username = RequireValue(bootstrap.Username, nameof(bootstrap.Username));
            string email = RequireValue(bootstrap.Email, nameof(bootstrap.Email)).ToLowerInvariant();
            string fullName = RequireValue(bootstrap.FullName, nameof(bootstrap.FullName));

            User? user = await dbContext
                .Set<User>()
                .Include(x => x.PasswordCredential)
                .Include(x => x.UserRoles)
                .FirstOrDefaultAsync(x => x.UserCode == userCode, cancellationToken);
            bool userCreated = user is null;
            if (user is null)
            {
                bool identityAlreadyUsed = await dbContext
                    .Set<User>()
                    .AnyAsync(
                        x => x.Username.ToUpper() == username.ToUpper()
                            || x.Email == email,
                        cancellationToken
                    );
                if (identityAlreadyUsed)
                {
                    throw new InvalidOperationException(
                        "Admin bootstrap username or email is already assigned to another user."
                    );
                }

                string initialPassword = RequireValue(
                    bootstrap.InitialPassword,
                    nameof(bootstrap.InitialPassword)
                );
                passwordPolicy.Validate(initialPassword);

                user = User.Create(
                    userCode,
                    username,
                    email,
                    fullName,
                    null,
                    null,
                    null,
                    null,
                    "System-provisioned administrator"
                );
                string passwordHash = passwordHasher.HashPassword(
                    CredentialMarker,
                    initialPassword
                );
                user.SetPasswordCredential(UserPassword.Create(user, passwordHash));
                await dbContext.Set<User>().AddAsync(user, cancellationToken);

                logger.LogInformation(
                    "Provisioned initial system administrator with user code {UserCode}; password change is required on first use.",
                    userCode
                );
            }
            else if (
                !string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new InvalidOperationException(
                    $"Bootstrap user code '{userCode}' exists but its username or email does not match configuration."
                );
            }

            user.AssignRole(superAdmin.Id);
            if (userCreated)
            {
                await audit.WriteAsync(
                    new(
                        "SYSTEM_ADMIN_BOOTSTRAPPED",
                        "SUCCESS",
                        TargetType: "USER",
                        TargetId: user.UserCode
                    ),
                    cancellationToken
                );
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string RequireValue(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Admin bootstrap configuration '{propertyName}' is required."
            );
        }

        return value.Trim();
    }
}
