namespace Friday.Modules.Admin.Domain.Audit;

public sealed class SecurityAuditEvent
{
    private SecurityAuditEvent() { }

    public long Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public int? ActorUserId { get; private set; }
    public string? TargetType { get; private set; }
    public string? TargetId { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public string? ReasonCode { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? TraceId { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public string? MetadataJson { get; private set; }

    public static SecurityAuditEvent Create(
        string eventType,
        int? actorUserId,
        string outcome,
        DateTime occurredOnUtc,
        string? targetType = null,
        string? targetId = null,
        string? reasonCode = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? traceId = null,
        string? metadataJson = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        return new SecurityAuditEvent
        {
            EventType = eventType.Trim().ToUpperInvariant(),
            ActorUserId = actorUserId,
            TargetType = targetType?.Trim(),
            TargetId = targetId?.Trim(),
            Outcome = outcome.Trim().ToUpperInvariant(),
            ReasonCode = reasonCode?.Trim(),
            IpAddress = ipAddress?.Trim(),
            UserAgent = userAgent?.Trim(),
            TraceId = traceId?.Trim(),
            OccurredOnUtc = occurredOnUtc,
            MetadataJson = metadataJson,
        };
    }
}
