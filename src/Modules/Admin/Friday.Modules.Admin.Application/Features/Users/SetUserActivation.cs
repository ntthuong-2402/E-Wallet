using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Auditing;
using Friday.Modules.Admin.Application.Authorization;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;

namespace Friday.Modules.Admin.Application.Features.Users;

public sealed record SetUserActivationCommand(int UserId, bool IsActive) : ICommand<UserDto>;

public sealed class SetUserActivationHandler(
    IUserRepository users,
    IUserSessionRepository sessions,
    ISecurityAuditWriter audit,
    IPrivilegedAccountGuard privilegedAccountGuard
) : ICommandHandler<SetUserActivationCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(SetUserActivationCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new FridayException(ErrorCodes.Admin.UserNotFound, "User was not found.", StatusCodes.Status404NotFound);
        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            await privilegedAccountGuard.EnsureCanDisableAsync(user.Id, cancellationToken);
            user.Deactivate();
            await sessions.RevokeAllForUserAsync(user.Id, cancellationToken);
        }

        await audit.WriteAsync(new(request.IsActive ? "USER_ACTIVATED" : "USER_DEACTIVATED", "SUCCESS", TargetType: "USER", TargetId: user.Id.ToString()), cancellationToken);
        return UserDto.FromUser(user);
    }
}
