using Friday.BuildingBlocks.Application.Abstractions;
using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Configuration;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using Friday.Modules.Admin.Domain.Security;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Friday.Modules.Admin.Application.Features.Auth;

public sealed record RegisterCommand(
    string Username,
    string Email,
    string Password,
    string FullName,
    string? Phone
) : ICommand<LoginResponseDto>;

public sealed class RegisterCommandHandler(
    IUserRepository users,
    IPasswordHasher<CredentialUser> passwordHasher,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    IOptionsMonitor<RegistrationOptions> registrationOptions
) : ICommandHandler<RegisterCommand, LoginResponseDto>
{
    private static readonly CredentialUser CredentialMarker = new();

    public async Task<LoginResponseDto> HandleAsync(
        RegisterCommand request,
        CancellationToken cancellationToken
    )
    {
        if (!registrationOptions.CurrentValue.AllowPublicRegistration)
        {
            throw new FridayException(
                ErrorCodes.Admin.RegistrationDisabled,
                "Public registration is disabled.",
                StatusCodes.Status403Forbidden
            );
        }

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

        string userCode = await GenerateUniqueUserCodeAsync(users, cancellationToken);

        User user = User.Create(
            userCode,
            request.Username,
            request.Email,
            request.FullName,
            request.Phone,
            null,
            null,
            null,
            null
        );

        string hash = passwordHasher.HashPassword(CredentialMarker, request.Password);
        user.SetPasswordCredential(UserPassword.Create(user, hash));

        await users.AddAsync(user, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return await mediator.SendAsync(
            new LoginCommand(user.Username, request.Password),
            cancellationToken
        );
    }

    private static async Task<string> GenerateUniqueUserCodeAsync(
        IUserRepository users,
        CancellationToken cancellationToken
    )
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            string code = "U" + Guid.NewGuid().ToString("N")[..15].ToUpperInvariant();
            if (!await users.ExistsByUserCodeAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not allocate a unique user code.");
    }
}
