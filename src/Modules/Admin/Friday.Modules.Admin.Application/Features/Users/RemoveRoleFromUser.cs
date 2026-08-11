using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Auditing;
using Friday.Modules.Admin.Application.Authorization;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;

namespace Friday.Modules.Admin.Application.Features.Users;

public sealed record RemoveRoleFromUserCommand(int UserId, int RoleId) : ICommand<UserDto>;

public sealed class RemoveRoleFromUserHandler(
    IUserRepository users,
    IRoleRepository roles,
    IUserSessionRepository sessions,
    IPrivilegedAccountGuard privilegedAccountGuard,
    ISecurityAuditWriter audit
) : ICommandHandler<RemoveRoleFromUserCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(
        RemoveRoleFromUserCommand request,
        CancellationToken cancellationToken
    )
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new FridayException(
                ErrorCodes.Admin.UserNotFound,
                $"User '{request.UserId}' was not found.",
                StatusCodes.Status404NotFound
            );
        var role = await roles.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new FridayException(
                ErrorCodes.Admin.RoleNotFound,
                $"Role '{request.RoleId}' was not found.",
                StatusCodes.Status404NotFound
            );

        await privilegedAccountGuard.EnsureCanRemoveRoleAsync(
            user.Id,
            role.Id,
            cancellationToken
        );
        user.RemoveRole(role.Id);
        await sessions.RevokeAllForUserAsync(user.Id, cancellationToken);
        await audit.WriteAsync(
            new(
                "ROLE_REMOVED",
                "SUCCESS",
                TargetType: "USER",
                TargetId: user.Id.ToString(),
                MetadataJson: $"{{\"roleId\":{role.Id}}}"
            ),
            cancellationToken
        );

        return UserDto.FromUser(user);
    }
}
