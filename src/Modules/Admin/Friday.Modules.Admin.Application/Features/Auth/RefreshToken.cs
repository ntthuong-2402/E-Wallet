using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Admin.Application.Configuration;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Application.Security;
using Friday.Modules.Admin.Application.Auditing;
using Friday.BuildingBlocks.Application.Abstractions;
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
    IOptions<JwtSettings> jwtSettings,
    ISecurityAuditWriter audit,
    IUnitOfWork unitOfWork
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
            UserSession? consumed = await sessions.GetByRefreshTokenHashAsync(hash, cancellationToken);
            if (consumed?.ReplacedAtUtc is not null)
            {
                consumed.MarkReuseDetected();
                await sessions.RevokeFamilyAsync(consumed.TokenFamilyId, cancellationToken);
                await audit.WriteAsync(new("REFRESH_TOKEN_REUSE_DETECTED", "FAILURE", ActorUserId: consumed.UserId, TargetType: "TOKEN_FAMILY", TargetId: consumed.TokenFamilyId.ToString(), ReasonCode: ErrorCodes.Admin.InvalidRefreshToken), cancellationToken);
            }
            else
            {
                await audit.WriteAsync(
                    new(
                        "REFRESH_TOKEN_REJECTED",
                        "FAILURE",
                        ActorUserId: consumed?.UserId,
                        TargetType: "SESSION",
                        TargetId: consumed?.Id.ToString(),
                        ReasonCode: ErrorCodes.Admin.InvalidRefreshToken
                    ),
                    cancellationToken
                );
            }

            await unitOfWork.CommitAsync(cancellationToken);

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
            await audit.WriteAsync(
                new(
                    "REFRESH_TOKEN_REJECTED",
                    "FAILURE",
                    ActorUserId: session.UserId,
                    TargetType: "SESSION",
                    TargetId: session.Id.ToString(),
                    ReasonCode: ErrorCodes.Admin.SessionInvalid
                ),
                cancellationToken
            );
            await unitOfWork.CommitAsync(cancellationToken);
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
        UserSession replacement = session.ReplaceWith(newHash, DateTime.UtcNow.AddDays(refreshDays));
        await sessions.AddAsync(replacement, cancellationToken);

        JwtAccessTokenResult access = jwt.CreateAccessToken(user.Id, replacement.Id, roleCodes);

        try
        {
            await audit.WriteAsync(
                new(
                    "REFRESH_TOKEN_ROTATED",
                    "SUCCESS",
                    ActorUserId: user.Id,
                    TargetType: "SESSION",
                    TargetId: replacement.Id.ToString(),
                    MetadataJson: $"{{\"tokenFamilyId\":\"{replacement.TokenFamilyId}\"}}"
                ),
                cancellationToken
            );
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            throw new FridayException(
                ErrorCodes.Admin.InvalidRefreshToken,
                "Refresh token was already consumed.",
                StatusCodes.Status401Unauthorized
            );
        }

        return new RefreshTokenResponseDto(
            access.Token,
            access.ExpiresAtUtc,
            newRefresh
        );
    }
}
