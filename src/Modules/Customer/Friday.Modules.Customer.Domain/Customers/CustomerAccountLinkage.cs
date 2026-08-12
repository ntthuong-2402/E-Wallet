namespace Friday.Modules.Customer.Domain.Customers;

public sealed class CustomerAccountLinkage
{
    private CustomerAccountLinkage() { }

    public long Id { get; private set; }
    public int CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;
    public string AccountId { get; private set; } = string.Empty;
    public string LinkedByActorUserId { get; private set; } = string.Empty;
    public DateTime LinkedOnUtc { get; private set; }
    public string LinkReason { get; private set; } = string.Empty;
    public string? UnlinkedByActorUserId { get; private set; }
    public DateTime? UnlinkedOnUtc { get; private set; }
    public string? UnlinkReason { get; private set; }

    public bool IsActive => UnlinkedOnUtc is null;

    public static CustomerAccountLinkage Link(
        Customer customer,
        string accountId,
        string actorUserId,
        string reason,
        DateTime linkedOnUtc
    )
    {
        ArgumentNullException.ThrowIfNull(customer);
        EnsureUtc(linkedOnUtc, nameof(linkedOnUtc));
        return new CustomerAccountLinkage
        {
            Customer = customer,
            AccountId = NormalizeAccountId(accountId),
            LinkedByActorUserId = Require(actorUserId, nameof(actorUserId), 128),
            LinkReason = Require(reason, nameof(reason), 500),
            LinkedOnUtc = linkedOnUtc,
        };
    }

    public void Unlink(string actorUserId, string reason, DateTime unlinkedOnUtc)
    {
        if (!IsActive)
            throw new InvalidOperationException("Customer account linkage is already inactive.");
        EnsureUtc(unlinkedOnUtc, nameof(unlinkedOnUtc));
        if (unlinkedOnUtc < LinkedOnUtc)
            throw new ArgumentException("Unlink timestamp cannot precede link timestamp.", nameof(unlinkedOnUtc));

        UnlinkedByActorUserId = Require(actorUserId, nameof(actorUserId), 128);
        UnlinkReason = Require(reason, nameof(reason), 500);
        UnlinkedOnUtc = unlinkedOnUtc;
    }

    public static string NormalizeAccountId(string value) => Require(value, nameof(value), 128);

    private static string Require(string value, string name, int maxLength)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 || normalized.Length > maxLength)
            throw new ArgumentException($"{name} must contain 1 to {maxLength} characters.", name);
        if (normalized.Any(char.IsControl))
            throw new ArgumentException($"{name} cannot contain control characters.", name);
        return normalized;
    }

    private static void EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Account linkage timestamp must be UTC.", name);
    }
}
