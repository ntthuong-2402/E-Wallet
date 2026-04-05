using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Configuration;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Application.Security;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using Friday.Modules.Admin.Domain.Security;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Friday.Modules.Admin.Application.Features.Auth;

public sealed record LoginCommand(string Login, string Password) : ICommand<LoginResponseDto>;

public sealed class LoginCommandHandler(
    IUserRepository users,
    IUserSessionRepository sessions,
    IRoleRepository roles,
    IPasswordHasher<CredentialUser> passwordHasher,
    IJwtTokenIssuer jwt,
    IOptions<JwtSettings> jwtSettings,
    IHttpContextAccessor httpContextAccessor
) : ICommandHandler<LoginCommand, LoginResponseDto>
{
    private static readonly CredentialUser CredentialMarker = new();

    public async Task<LoginResponseDto> HandleAsync(
        LoginCommand request,
        CancellationToken cancellationToken
    )
    {
        User? user = await users.GetByLoginWithPasswordAsync(request.Login, cancellationToken);

        if (user?.PasswordCredential is null)
        {
            throw new FridayException(
                ErrorCodes.Admin.InvalidCredentials,
                "Invalid login or password.",
                StatusCodes.Status401Unauthorized
            );
        }

        PasswordVerificationResult verification = passwordHasher.VerifyHashedPassword(
            CredentialMarker,
            user.PasswordCredential.PasswordHash,
            request.Password
        );

        if (verification == PasswordVerificationResult.Failed)
        {
            throw new FridayException(
                ErrorCodes.Admin.InvalidCredentials,
                "Invalid login or password.",
                StatusCodes.Status401Unauthorized
            );
        }

        if (!user.IsActive)
        {
            throw new FridayException(
                ErrorCodes.Admin.UserInactive,
                "User account is inactive.",
                StatusCodes.Status403Forbidden
            );
        }

        if (user.IsLocked)
        {
            throw new FridayException(
                ErrorCodes.Admin.UserLockedAuth,
                "User account is locked.",
                StatusCodes.Status403Forbidden
            );
        }

        int[] roleIds = user.UserRoles.Select(x => x.RoleId).ToArray();
        IReadOnlyList<Domain.Aggregates.RoleAggregate.Role> roleEntities =
            await roles.GetByIdsAsync(roleIds, cancellationToken);
        string[] roleCodes = roleEntities
            .Where(x => x.IsActive)
            .Select(x => x.Code)
            .Distinct()
            .ToArray();

        string refreshToken = RefreshTokenUtilities.GenerateOpaqueToken();
        string refreshHash = RefreshTokenUtilities.Hash(refreshToken);
        int refreshDays = Math.Clamp(jwtSettings.Value.RefreshTokenDays, 1, 365);
        DateTime refreshExpires = DateTime.UtcNow.AddDays(refreshDays);

        HttpContext? http = httpContextAccessor.HttpContext;
        string? ip = http?.Connection.RemoteIpAddress?.ToString();
        string? ua = http?.Request.Headers.UserAgent.ToString();

        UserSession session = UserSession.Create(
            user.Id,
            refreshHash,
            refreshExpires,
            string.IsNullOrWhiteSpace(ip) ? null : ip,
            string.IsNullOrWhiteSpace(ua) ? null : ua
        );

        await sessions.AddAsync(session, cancellationToken);

        JwtAccessTokenResult access = jwt.CreateAccessToken(user.Id, session.Id, roleCodes);

        return new LoginResponseDto(
            access.Token,
            access.ExpiresAtUtc,
            refreshToken,
            UserDto.FromUser(user)
        );
    }
}
