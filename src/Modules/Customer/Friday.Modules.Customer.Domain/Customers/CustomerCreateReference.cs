namespace Friday.Modules.Customer.Domain.Customers;

public sealed class CustomerCreateReference
{
    private CustomerCreateReference() { }

    public long Id { get; private set; }
    public string RefId { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public string ActorUserId { get; private set; } = string.Empty;
    public int CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;
    public string ResponseJson { get; private set; } = string.Empty;
    public DateTime CreatedOnUtc { get; private set; }

    public static CustomerCreateReference Create(
        string refId,
        string requestHash,
        string actorUserId,
        Customer customer,
        string responseJson,
        DateTime createdOnUtc
    )
    {
        ArgumentNullException.ThrowIfNull(customer);
        if (createdOnUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Reference timestamp must be UTC.", nameof(createdOnUtc));

        return new CustomerCreateReference
        {
            RefId = NormalizeRefId(refId),
            RequestHash = Require(requestHash, nameof(requestHash)),
            ActorUserId = Require(actorUserId, nameof(actorUserId)),
            Customer = customer,
            ResponseJson = Require(responseJson, nameof(responseJson)),
            CreatedOnUtc = createdOnUtc,
        };
    }

    public static string NormalizeRefId(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 100)
            throw new ArgumentException("RefId must contain 1 to 100 characters.", nameof(value));
        if (normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or ':' or '-')))
            throw new ArgumentException(
                "RefId may contain only ASCII letters, digits, dot, underscore, colon, or hyphen.",
                nameof(value)
            );
        return normalized;
    }

    private static string Require(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", name)
            : value;
}
