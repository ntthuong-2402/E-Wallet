namespace Friday.Modules.Admin.Application.Auditing;

public sealed record SecurityAuditRecord(
    string EventType,
    string Outcome,
    int? ActorUserId = null,
    string? TargetType = null,
    string? TargetId = null,
    string? ReasonCode = null,
    string? MetadataJson = null
);
