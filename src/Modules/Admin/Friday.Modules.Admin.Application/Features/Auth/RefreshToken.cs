using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Configuration;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Application.Security;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Friday.Modules.Admin.Application.Features.Auth;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<RefreshTokenResponseDto>;

public sealed class RefreshTokenCommandHandler(
    IUserRepository users,
    IUserSessionRepository sessions,
    IRoleRepository roles,
    IJwtTokenIssuer jwt,
    IOptions<JwtSettings> jwtSettings
) : ICommandHandler<RefreshTokenCommand, RefreshTokenResponseDto>
{
    public async Task<RefreshTokenResponseDto> HandleAsync(
        RefreshTokenCommand request,
        CancellationToken cancellationToken
    )
    {
        string hash = RefreshTokenUtilities.Hash(request.RefreshToken);
        UserSession? session = await sessions.GetActiveByRefreshTokenHashAsync(
            hash,
            cancellationToken
        );

        if (session is null)
        {
            throw new FridayException(
                ErrorCodes.Admin.InvalidRefreshToken,
                "Refresh token is invalid or expired.",
                StatusCodes.Status401Unauthorized
            );
        }

        User? user = await users.GetByIdAsync(
            session.UserId,
            cancellationToken
        );

        if (user is null || !user.IsActive || user.IsLocked)
        {
            throw new FridayException(
                ErrorCodes.Admin.SessionInvalid,
                "Session is no longer valid.",
                StatusCodes.Status401Unauthorized
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

        string newRefresh = RefreshTokenUtilities.GenerateOpaqueToken();
        string newHash = RefreshTokenUtilities.Hash(newRefresh);
        int refreshDays = Math.Clamp(jwtSettings.Value.RefreshTokenDays, 1, 365);
        session.RotateRefresh(newHash, DateTime.UtcNow.AddDays(refreshDays));

        JwtAccessTokenResult access = jwt.CreateAccessToken(user.Id, session.Id, roleCodes);

        return new RefreshTokenResponseDto(
            access.Token,
            access.ExpiresAtUtc,
            newRefresh
        );
    }
}
