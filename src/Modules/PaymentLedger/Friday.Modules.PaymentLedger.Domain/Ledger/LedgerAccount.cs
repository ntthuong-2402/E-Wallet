namespace Friday.Modules.PaymentLedger.Domain.Ledger;

public enum LedgerAccountStatus { Active, Suspended }

public sealed class LedgerAccount
{
    private LedgerAccount() { }

    public Guid Id { get; private set; }
    public string AccountRef { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public LedgerAccountStatus Status { get; private set; }
    public decimal AvailableBalance { get; private set; }
    public long Version { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime UpdatedOnUtc { get; private set; }

    public static LedgerAccount Open(string accountRef, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(accountRef)) throw new ArgumentException("AccountRef is required.", nameof(accountRef));
        string normalized = accountRef.Trim();
        if (normalized.Length > 100 || normalized.Any(char.IsControl)) throw new ArgumentException("AccountRef must be at most 100 characters and contain no control characters.", nameof(accountRef));
        EnsureUtc(nowUtc);
        return new LedgerAccount { Id = Guid.NewGuid(), AccountRef = normalized, Currency = "VND", Status = LedgerAccountStatus.Active, CreatedOnUtc = nowUtc, UpdatedOnUtc = nowUtc };
    }

    public void Debit(decimal amount, DateTime nowUtc)
    {
        EnsureCanPost(amount, nowUtc);
        if (AvailableBalance < amount) throw new InvalidOperationException("Ledger account has insufficient available funds.");
        AvailableBalance -= amount;
        Version++;
        UpdatedOnUtc = nowUtc;
    }

    public void Credit(decimal amount, DateTime nowUtc)
    {
        EnsureCanPost(amount, nowUtc);
        AvailableBalance += amount;
        Version++;
        UpdatedOnUtc = nowUtc;
    }

    private void EnsureCanPost(decimal amount, DateTime nowUtc)
    {
        EnsureUtc(nowUtc);
        if (Status != LedgerAccountStatus.Active) throw new InvalidOperationException("Ledger account is not active.");
        if (amount <= 0 || decimal.Truncate(amount) != amount) throw new ArgumentOutOfRangeException(nameof(amount), "VND amount must be a positive whole number.");
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.");
    }
}
