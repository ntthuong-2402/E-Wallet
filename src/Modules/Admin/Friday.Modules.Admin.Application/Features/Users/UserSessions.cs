using Friday.Modules.Admin.Application.Auditing;
using Friday.Modules.Admin.Application.Models;
using Friday.Modules.Admin.Domain.Repositories;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Admin.Application.Features.Users;

public sealed record GetUserSessionsQuery(int UserId) : IQuery<IReadOnlyList<UserSessionDto>>;

public sealed class GetUserSessionsHandler(IUserSessionRepository sessions)
    : IQueryHandler<GetUserSessionsQuery, IReadOnlyList<UserSessionDto>>
{
    public async Task<IReadOnlyList<UserSessionDto>> HandleAsync(GetUserSessionsQuery request, CancellationToken cancellationToken)
        => (await sessions.ListForUserAsync(request.UserId, cancellationToken)).Select(UserSessionDto.FromSession).ToArray();
}

public sealed record RevokeUserSessionsCommand(int UserId) : ICommand<bool>;

public sealed class RevokeUserSessionsHandler(IUserSessionRepository sessions, ISecurityAuditWriter audit)
    : ICommandHandler<RevokeUserSessionsCommand, bool>
{
    public async Task<bool> HandleAsync(RevokeUserSessionsCommand request, CancellationToken cancellationToken)
    {
        await sessions.RevokeAllForUserAsync(request.UserId, cancellationToken);
        await audit.WriteAsync(new("SESSIONS_REVOKED", "SUCCESS", TargetType: "USER", TargetId: request.UserId.ToString()), cancellationToken);
        return true;
    }
}
