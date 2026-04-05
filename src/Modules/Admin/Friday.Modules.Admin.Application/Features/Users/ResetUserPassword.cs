using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using Friday.Modules.Admin.Domain.Security;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Friday.Modules.Admin.Application.Features.Users;

public sealed record ResetUserPasswordRequest(string NewPassword);

public sealed record ResetUserPasswordCommand(int UserId, string NewPassword) : ICommand<UserDto>;

public sealed class ResetUserPasswordHandler(
    IUserRepository users,
    IPasswordHasher<CredentialUser> passwordHasher
) : ICommandHandler<ResetUserPasswordCommand, UserDto>
{
    private static readonly CredentialUser CredentialMarker = new();

    public async Task<UserDto> HandleAsync(
        ResetUserPasswordCommand request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new FridayException(ErrorCodes.Admin.PasswordRequired, "Password is required.");
        }

        Domain.Aggregates.UserAggregate.User? user = await users.GetByIdWithPasswordAsync(
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

        string hash = passwordHasher.HashPassword(CredentialMarker, request.NewPassword);
        user.SetOrUpdatePasswordHash(hash);

        return UserDto.FromUser(user);
    }
}
