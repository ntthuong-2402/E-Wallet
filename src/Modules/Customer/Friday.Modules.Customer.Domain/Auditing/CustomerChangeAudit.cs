namespace Friday.Modules.Customer.Domain.Auditing;

using Friday.Modules.Customer.Domain.Customers;
using CustomerAggregate = Friday.Modules.Customer.Domain.Customers.Customer;

public sealed class CustomerChangeAudit
{
    private CustomerChangeAudit() { }

    public long Id { get; private set; }
    public int CustomerId { get; private set; }
    public CustomerAggregate Customer { get; private set; } = null!;
    public string CustomerCode { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string ActorUserId { get; private set; } = string.Empty;
    public string ChangedFieldsJson { get; private set; } = "{}";
    public string? FromStatus { get; private set; }
    public string? ToStatus { get; private set; }
    public string? Reason { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public string TraceId { get; private set; } = string.Empty;
    public DateTime OccurredOnUtc { get; private set; }
    public DateTime RetainUntilUtc { get; private set; }

    public static CustomerChangeAudit Create(
        CustomerAggregate customer,
        string eventType,
        string actorUserId,
        CustomerAuditChangeSet changes,
        string? fromStatus,
        string? toStatus,
        string? reason,
        string outcome,
        string traceId,
        DateTime occurredOnUtc,
        DateTime retainUntilUtc
    )
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(changes);
        if (occurredOnUtc.Kind != DateTimeKind.Utc || retainUntilUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Audit timestamps must be UTC.");
        if (retainUntilUtc <= occurredOnUtc)
            throw new ArgumentException("Audit retention must end after occurrence.", nameof(retainUntilUtc));

        return new CustomerChangeAudit
        {
            Customer = customer,
            CustomerCode = customer.CustomerCode,
            EventType = Require(eventType, nameof(eventType)),
            ActorUserId = Require(actorUserId, nameof(actorUserId)),
            ChangedFieldsJson = changes.Json,
            FromStatus = Optional(fromStatus),
            ToStatus = Optional(toStatus),
            Reason = Optional(reason),
            Outcome = Require(outcome, nameof(outcome)),
            TraceId = Require(traceId, nameof(traceId)),
            OccurredOnUtc = occurredOnUtc,
            RetainUntilUtc = retainUntilUtc,
        };
    }

    private static string Require(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value.Trim();

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
