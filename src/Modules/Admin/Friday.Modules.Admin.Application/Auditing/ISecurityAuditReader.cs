using Friday.Modules.Admin.Application.Models;

namespace Friday.Modules.Admin.Application.Auditing;

public interface ISecurityAuditReader
{
    Task<IReadOnlyList<SecurityAuditEventDto>> ListAsync(
        int skip,
        int take,
        string? eventType,
        int? actorUserId,
        string? targetType,
        string? targetId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default
    );
}
