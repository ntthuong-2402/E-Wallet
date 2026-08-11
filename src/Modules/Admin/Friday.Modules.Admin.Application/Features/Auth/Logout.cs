using Friday.Modules.Admin.Application.Security;
using Friday.Modules.Admin.Application.Auditing;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Admin.Application.Features.Auth;

public sealed record LogoutCommand(string RefreshToken) : ICommand<bool>;

public sealed class LogoutCommandHandler(
    IUserSessionRepository sessions,
    ISecurityAuditWriter audit
)
    : ICommandHandler<LogoutCommand, bool>
{
    public async Task<bool> HandleAsync(LogoutCommand request, CancellationToken cancellationToken)
    {
        string hash = RefreshTokenUtilities.Hash(request.RefreshToken);
        UserSession? session =
            await sessions.GetByRefreshTokenHashAsync(hash, cancellationToken);

        if (session is not null && session.RevokedAtUtc is null)
        {
            session.Revoke();
            await audit.WriteAsync(
                new(
                    "LOGOUT_SUCCEEDED",
                    "SUCCESS",
                    ActorUserId: session.UserId,
                    TargetType: "SESSION",
                    TargetId: session.Id.ToString()
                ),
                cancellationToken
            );
        }

        return true;
    }
}
