using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Friday.Modules.Admin.Application.Auditing;
using Friday.BuildingBlocks.Application.Errors;
using Microsoft.AspNetCore.Http;

namespace Friday.Modules.Admin.Application.Authorization;

public sealed class PermissionAuthorizationHandler(
    IUserPermissionReader permissions,
    ISecurityAuditWriter audit,
    IHttpContextAccessor httpContextAccessor
)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement
    )
    {
        string? subject =
            context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (
            int.TryParse(subject, out int userId)
            && await permissions.HasPermissionAsync(userId, requirement.Permission)
        )
        {
            context.Succeed(requirement);
            return;
        }

        await audit.WriteImmediateAsync(
            new SecurityAuditRecord(
                "PERMISSION_DENIED",
                "FAILURE",
                int.TryParse(subject, out int deniedUserId) ? deniedUserId : null,
                "PERMISSION",
                requirement.Permission,
                ErrorCodes.Admin.PermissionDenied
            ),
            httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None
        );
    }
}
