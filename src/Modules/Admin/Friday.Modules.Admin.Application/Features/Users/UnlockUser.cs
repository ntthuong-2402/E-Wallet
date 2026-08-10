using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Auditing;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;

namespace Friday.Modules.Admin.Application.Features.Users;

public sealed record UnlockUserCommand(int UserId) : ICommand<UserDto>;

public sealed class UnlockUserHandler(
    IUserRepository users,
    ISecurityAuditWriter audit
) : ICommandHandler<UnlockUserCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(UnlockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new FridayException(ErrorCodes.Admin.UserNotFound, "User was not found.", StatusCodes.Status404NotFound);
        user.Unlock();
        await audit.WriteAsync(new("USER_UNLOCKED", "SUCCESS", TargetType: "USER", TargetId: user.Id.ToString()), cancellationToken);
        return UserDto.FromUser(user);
    }
}
