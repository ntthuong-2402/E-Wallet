namespace Friday.Modules.PaymentLedger.Domain.Ledger;

public enum FinancialTransactionType { InternalTransfer, Reversal }
public enum FinancialTransactionStatus { Posted, Reversed }
public enum LedgerEntryDirection { Debit, Credit }

public sealed class FinancialTransaction
{
    private FinancialTransaction() { }
    public Guid Id { get; private set; }
    public FinancialTransactionType Type { get; private set; }
    public FinancialTransactionStatus Status { get; private set; }
    public Guid SourceAccountId { get; private set; }
    public Guid DestinationAccountId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? OriginalTransactionId { get; private set; }
    public Guid? ReversalTransactionId { get; private set; }
    public DateTime PostedOnUtc { get; private set; }
    public Journal Journal { get; private set; } = null!;

    public static FinancialTransaction PostInternalTransfer(LedgerAccount source, LedgerAccount destination, decimal amount, string currency, string? description, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(destination);
        if (source.Id == destination.Id) throw new ArgumentException("Source and destination accounts must differ.");
        if (!string.Equals(currency?.Trim(), "VND", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Currency must be VND.", nameof(currency));
        ValidateDescription(description);
        source.Debit(amount, nowUtc);
        destination.Credit(amount, nowUtc);
        Guid id = Guid.NewGuid();
        return new FinancialTransaction
        {
            Id = id,
            Type = FinancialTransactionType.InternalTransfer,
            Status = FinancialTransactionStatus.Posted,
            SourceAccountId = source.Id,
            DestinationAccountId = destination.Id,
            Amount = amount,
            Currency = "VND",
            Description = NormalizeOptional(description),
            PostedOnUtc = nowUtc,
            Journal = Journal.Create(id, source.Id, destination.Id, amount, "VND", nowUtc)
        };
    }

    public FinancialTransaction Reverse(LedgerAccount originalSource, LedgerAccount originalDestination, string reason, DateTime nowUtc)
    {
        if (Type != FinancialTransactionType.InternalTransfer || Status != FinancialTransactionStatus.Posted || ReversalTransactionId.HasValue)
            throw new InvalidOperationException("Only an unreversed posted internal transfer can be reversed.");
        if (originalSource.Id != SourceAccountId || originalDestination.Id != DestinationAccountId)
            throw new ArgumentException("Reversal accounts do not match the original transaction.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reversal reason is required.", nameof(reason));
        originalDestination.Debit(Amount, nowUtc);
        originalSource.Credit(Amount, nowUtc);
        Guid reversalId = Guid.NewGuid();
        FinancialTransaction reversal = new()
        {
            Id = reversalId,
            Type = FinancialTransactionType.Reversal,
            Status = FinancialTransactionStatus.Posted,
            SourceAccountId = originalDestination.Id,
            DestinationAccountId = originalSource.Id,
            Amount = Amount,
            Currency = Currency,
            Description = NormalizeOptional(reason),
            OriginalTransactionId = Id,
            PostedOnUtc = nowUtc,
            Journal = Journal.Create(reversalId, originalDestination.Id, originalSource.Id, Amount, Currency, nowUtc)
        };
        Status = FinancialTransactionStatus.Reversed;
        ReversalTransactionId = reversalId;
        return reversal;
    }

    private static void ValidateDescription(string? value)
    {
        if (value is { Length: > 500 } || value?.Any(char.IsControl) == true) throw new ArgumentException("Description must be at most 500 characters and contain no control characters.", nameof(value));
    }
    private static string? NormalizeOptional(string? value) { ValidateDescription(value); return string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
}

public sealed class Journal
{
    private readonly List<JournalEntry> _entries = [];
    private Journal() { }
    public Guid Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public DateTime PostedOnUtc { get; private set; }
    public IReadOnlyCollection<JournalEntry> Entries => _entries;

    internal static Journal Create(Guid transactionId, Guid debitAccountId, Guid creditAccountId, decimal amount, string currency, DateTime nowUtc)
    {
        Journal journal = new() { Id = Guid.NewGuid(), TransactionId = transactionId, PostedOnUtc = nowUtc };
        journal._entries.Add(JournalEntry.Create(journal.Id, debitAccountId, LedgerEntryDirection.Debit, amount, currency));
        journal._entries.Add(JournalEntry.Create(journal.Id, creditAccountId, LedgerEntryDirection.Credit, amount, currency));
        if (journal._entries.Where(x => x.Direction == LedgerEntryDirection.Debit).Sum(x => x.Amount) != journal._entries.Where(x => x.Direction == LedgerEntryDirection.Credit).Sum(x => x.Amount))
            throw new InvalidOperationException("Journal debit and credit totals must balance.");
        return journal;
    }
}

public sealed class JournalEntry
{
    private JournalEntry() { }
    public Guid Id { get; private set; }
    public Guid JournalId { get; private set; }
    public Guid LedgerAccountId { get; private set; }
    public LedgerEntryDirection Direction { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    internal static JournalEntry Create(Guid journalId, Guid accountId, LedgerEntryDirection direction, decimal amount, string currency) =>
        new() { Id = Guid.NewGuid(), JournalId = journalId, LedgerAccountId = accountId, Direction = direction, Amount = amount, Currency = currency };
}
