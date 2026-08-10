using Friday.Modules.Admin.Application.Models;

namespace Friday.Modules.Admin.Application.Auditing;

public interface ISecurityAuditReader
{
    Task<IReadOnlyList<SecurityAuditEventDto>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default
    );
}
