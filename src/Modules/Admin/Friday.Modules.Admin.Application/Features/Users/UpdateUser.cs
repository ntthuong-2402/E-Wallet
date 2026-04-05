using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;

namespace Friday.Modules.Admin.Application.Features.Users;

public sealed record UpdateUserRequest(
    string UserCode,
    string Username,
    string Email,
    string FullName,
    string? Phone,
    string? Address,
    string? CompanyName,
    string? JobTitle,
    string? Notes
);

public sealed record UpdateUserCommand(
    int UserId,
    string UserCode,
    string Username,
    string Email,
    string FullName,
    string? Phone,
    string? Address,
    string? CompanyName,
    string? JobTitle,
    string? Notes
) : ICommand<UserDto>;

public sealed class UpdateUserHandler(IUserRepository users)
    : ICommandHandler<UpdateUserCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(
        UpdateUserCommand request,
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

        string normalizedCode = request.UserCode.Trim().ToUpperInvariant();
        if (
            !string.Equals(user.UserCode, normalizedCode, StringComparison.Ordinal)
            && await users.ExistsByUserCodeAsync(request.UserCode, cancellationToken)
        )
        {
            throw new FridayException(ErrorCodes.Admin.UserCodeExists, "User code already exists.");
        }

        string normalizedUsername = request.Username.Trim();
        if (
            !string.Equals(
                user.Username,
                normalizedUsername,
                StringComparison.OrdinalIgnoreCase
            ) && await users.ExistsByUsernameAsync(request.Username, cancellationToken)
        )
        {
            throw new FridayException(
                ErrorCodes.Admin.UserUsernameExists,
                "Username already exists."
            );
        }

        string normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (
            !string.Equals(user.Email, normalizedEmail, StringComparison.Ordinal)
            && await users.ExistsByEmailAsync(request.Email, cancellationToken)
        )
        {
            throw new FridayException(ErrorCodes.Admin.UserEmailExists, "Email already exists.");
        }

        user.ChangeUserCode(request.UserCode);
        user.ChangeUsername(request.Username);
        user.ChangeEmail(request.Email);
        user.UpdateProfile(
            request.FullName,
            request.Phone,
            request.Address,
            request.CompanyName,
            request.JobTitle,
            request.Notes
        );

        return UserDto.FromUser(user);
    }
}
