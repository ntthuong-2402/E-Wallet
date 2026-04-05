using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using Friday.Modules.Admin.Domain.Security;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Identity;

namespace Friday.Modules.Admin.Application.Features.Users;

public sealed record CreateUserCommand(
    string UserCode,
    string Username,
    string Email,
    string FullName,
    string Password,
    string? Phone,
    string? Address,
    string? CompanyName,
    string? JobTitle,
    string? Notes,
    int[] RoleIds
) : ICommand<UserDto>;

public sealed class CreateUserHandler(
    IUserRepository users,
    IRoleRepository roles,
    IPasswordHasher<CredentialUser> passwordHasher
) : ICommandHandler<CreateUserCommand, UserDto>
{
    private static readonly CredentialUser CredentialMarker = new();

    public async Task<UserDto> HandleAsync(
        CreateUserCommand request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new FridayException(ErrorCodes.Admin.PasswordRequired, "Password is required.");
        }

        if (await users.ExistsByUsernameAsync(request.Username, cancellationToken))
        {
            throw new FridayException(
                ErrorCodes.Admin.UserUsernameExists,
                "Username already exists."
            );
        }

        if (await users.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            throw new FridayException(ErrorCodes.Admin.UserEmailExists, "Email already exists.");
        }

        if (await users.ExistsByUserCodeAsync(request.UserCode, cancellationToken))
        {
            throw new FridayException(ErrorCodes.Admin.UserCodeExists, "User code already exists.");
        }

        int[] roleIds = request.RoleIds.Distinct().ToArray();
        if (roleIds.Length > 0)
        {
            IReadOnlyList<Domain.Aggregates.RoleAggregate.Role> existingRoles =
                await roles.GetByIdsAsync(roleIds, cancellationToken);
            if (existingRoles.Count != roleIds.Length)
            {
                throw new FridayException(
                    ErrorCodes.Admin.UserRoleNotFound,
                    "Some roles are not found."
                );
            }
        }

        User user = User.Create(
            request.UserCode,
            request.Username,
            request.Email,
            request.FullName,
            request.Phone,
            request.Address,
            request.CompanyName,
            request.JobTitle,
            request.Notes
        );

        string hash = passwordHasher.HashPassword(CredentialMarker, request.Password);
        user.SetPasswordCredential(UserPassword.Create(user, hash));

        foreach (int roleId in roleIds)
        {
            user.AssignRole(roleId);
        }

        await users.AddAsync(user, cancellationToken);

        return UserDto.FromUser(user);
    }
}
