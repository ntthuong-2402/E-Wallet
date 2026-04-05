using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;

namespace Friday.Modules.Admin.Application.Features.Users;

public sealed record AssignRoleToUserCommand(int UserId, int RoleId) : ICommand<UserDto>;

public sealed class AssignRoleToUserHandler(IUserRepository users, IRoleRepository roles)
    : ICommandHandler<AssignRoleToUserCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(
        AssignRoleToUserCommand request,
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

        Domain.Aggregates.RoleAggregate.Role? role = await roles.GetByIdAsync(
            request.RoleId,
            cancellationToken
        );
        if (role is null || !role.IsActive)
        {
            throw new FridayException(
                ErrorCodes.Admin.RoleNotFound,
                $"Role '{request.RoleId}' was not found or inactive.",
                StatusCodes.Status404NotFound
            );
        }

        user.AssignRole(request.RoleId);

        return UserDto.FromUser(user);
    }
}
