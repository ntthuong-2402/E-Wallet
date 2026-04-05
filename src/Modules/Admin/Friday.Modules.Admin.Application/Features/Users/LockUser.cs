using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;

namespace Friday.Modules.Admin.Application.Features.Users;

public sealed record LockUserCommand(int UserId) : ICommand<UserDto>;

public sealed class LockUserHandler(IUserRepository users, IUserSessionRepository sessions)
    : ICommandHandler<LockUserCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(
        LockUserCommand request,
        CancellationToken cancellationToken
    )
    {
        Domain.Aggregates.UserAggregate.User? user = await users.GetByIdAsync(
            request.UserId,
            cancellationToken
        );
        if (user is null)
        {
            throw new FridayException(
                ErrorCodes.Admin.UserNotFound,
                $"User '{request.UserId}' was not found.",
                StatusCodes.Status404NotFound
            );
        }

        user.Lock();
        await sessions.RevokeAllForUserAsync(user.Id, cancellationToken);

        return UserDto.FromUser(user);
    }
}
