using Friday.Modules.Admin.Application.Security;
using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Admin.Application.Features.Auth;

public sealed record LogoutCommand(string RefreshToken) : ICommand<bool>;

public sealed class LogoutCommandHandler(IUserSessionRepository sessions)
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
        }

        return true;
    }
}
