namespace Friday.Modules.PaymentLedger.Domain.Ledger;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord() { }
    public long Id { get; private set; }
    public string ActorUserId { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;
    public string RefId { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public string ResponseJson { get; private set; } = string.Empty;
    public DateTime CreatedOnUtc { get; private set; }
    public static IdempotencyRecord Create(string actor, string operation, string refId, string hash, string response, DateTime nowUtc) => new()
    {
        ActorUserId = Require(actor, nameof(actor), 128),
        Operation = Require(operation, nameof(operation), 64),
        RefId = NormalizeRefId(refId),
        RequestHash = Require(hash, nameof(hash), 64),
        ResponseJson = Require(response, nameof(response), 16000),
        CreatedOnUtc = nowUtc.Kind == DateTimeKind.Utc ? nowUtc : throw new ArgumentException("Timestamp must be UTC.")
    };
    public static string NormalizeRefId(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 100 || normalized.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or ':' or '-')))
            throw new ArgumentException("RefId must contain 1 to 100 safe ASCII reference characters.", nameof(value));
        return normalized;
    }
    private static string Require(string value, string name, int max) => string.IsNullOrWhiteSpace(value) || value.Length > max ? throw new ArgumentException($"{name} is required and cannot exceed {max} characters.", name) : value;
}
