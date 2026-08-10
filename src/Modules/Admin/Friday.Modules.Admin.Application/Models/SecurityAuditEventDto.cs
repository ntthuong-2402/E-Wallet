namespace Friday.Modules.Admin.Application.Models;

public sealed record SecurityAuditEventDto(
    long Id,
    string EventType,
    int? ActorUserId,
    string? TargetType,
    string? TargetId,
    string Outcome,
    string? ReasonCode,
    string? IpAddress,
    string? TraceId,
    DateTime OccurredOnUtc
);
