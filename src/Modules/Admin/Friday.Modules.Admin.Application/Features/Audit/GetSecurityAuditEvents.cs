using Friday.Modules.Admin.Application.Auditing;
using Friday.Modules.Admin.Application.Models;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Admin.Application.Features.Audit;

public sealed record GetSecurityAuditEventsQuery(
    int Skip = 0,
    int Take = 50,
    string? EventType = null,
    int? ActorUserId = null,
    string? TargetType = null,
    string? TargetId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null
)
    : IQuery<IReadOnlyList<SecurityAuditEventDto>>;

public sealed class GetSecurityAuditEventsHandler(ISecurityAuditReader reader)
    : IQueryHandler<GetSecurityAuditEventsQuery, IReadOnlyList<SecurityAuditEventDto>>
{
    public Task<IReadOnlyList<SecurityAuditEventDto>> HandleAsync(
        GetSecurityAuditEventsQuery request,
        CancellationToken cancellationToken
    ) => reader.ListAsync(
        Math.Max(0, request.Skip),
        Math.Clamp(request.Take, 1, 200),
        request.EventType,
        request.ActorUserId,
        request.TargetType,
        request.TargetId,
        request.FromUtc,
        request.ToUtc,
        cancellationToken
    );
}
