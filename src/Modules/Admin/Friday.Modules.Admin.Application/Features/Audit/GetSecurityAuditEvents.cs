using Friday.Modules.Admin.Application.Auditing;
using Friday.Modules.Admin.Application.Models;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Admin.Application.Features.Audit;

public sealed record GetSecurityAuditEventsQuery(int Skip = 0, int Take = 50)
    : IQuery<IReadOnlyList<SecurityAuditEventDto>>;

public sealed class GetSecurityAuditEventsHandler(ISecurityAuditReader reader)
    : IQueryHandler<GetSecurityAuditEventsQuery, IReadOnlyList<SecurityAuditEventDto>>
{
    public Task<IReadOnlyList<SecurityAuditEventDto>> HandleAsync(
        GetSecurityAuditEventsQuery request,
        CancellationToken cancellationToken
    ) => reader.ListAsync(Math.Max(0, request.Skip), Math.Clamp(request.Take, 1, 200), cancellationToken);
}
