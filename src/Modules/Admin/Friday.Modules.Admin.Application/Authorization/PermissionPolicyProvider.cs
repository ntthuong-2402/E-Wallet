using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Friday.BuildingBlocks.Application.Authorization;

namespace Friday.Modules.Admin.Application.Authorization;

public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    private readonly HashSet<string> _permissions;

    public PermissionPolicyProvider(
        IOptions<AuthorizationOptions> options,
        IEnumerable<IPermissionContribution> contributions
    ) : base(options)
    {
        _permissions = contributions
            .SelectMany(x => x.Permissions)
            .ToHashSet(StringComparer.Ordinal);
    }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (_permissions.Contains(policyName))
        {
            AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return base.GetPolicyAsync(policyName);
    }
}
