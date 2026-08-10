namespace Friday.Modules.Admin.Application.Auditing;

public interface ISecurityAuditWriter
{
    Task WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default);
}
