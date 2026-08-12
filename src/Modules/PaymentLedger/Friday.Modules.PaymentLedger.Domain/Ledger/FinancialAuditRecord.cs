namespace Friday.Modules.PaymentLedger.Domain.Ledger;

public sealed class FinancialAuditRecord
{
    private FinancialAuditRecord() { }

    public long Id { get; private set; }
    public string ActorUserId { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string RefId { get; private set; } = string.Empty;
    public Guid? LedgerAccountId { get; private set; }
    public Guid? FinancialTransactionId { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public string TraceId { get; private set; } = string.Empty;
    public DateTime OccurredOnUtc { get; private set; }

    public static FinancialAuditRecord Success(
        string actorUserId,
        string action,
        string refId,
        Guid? ledgerAccountId,
        Guid? financialTransactionId,
        string traceId,
        DateTime occurredOnUtc
    )
    {
        if (occurredOnUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Audit timestamp must be UTC.", nameof(occurredOnUtc));
        return new FinancialAuditRecord
        {
            ActorUserId = Require(actorUserId, nameof(actorUserId), 128),
            Action = Require(action, nameof(action), 64),
            RefId = IdempotencyRecord.NormalizeRefId(refId),
            LedgerAccountId = ledgerAccountId,
            FinancialTransactionId = financialTransactionId,
            Outcome = "SUCCESS",
            TraceId = Require(traceId, nameof(traceId), 128),
            OccurredOnUtc = occurredOnUtc,
        };
    }

    private static string Require(string value, string name, int max) =>
        string.IsNullOrWhiteSpace(value) || value.Length > max
            ? throw new ArgumentException($"{name} is required and cannot exceed {max} characters.", name)
            : value;
}
