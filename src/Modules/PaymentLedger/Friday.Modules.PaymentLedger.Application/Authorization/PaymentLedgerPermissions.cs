using Friday.BuildingBlocks.Application.Authorization;

namespace Friday.Modules.PaymentLedger.Application.Authorization;

public static class PaymentLedgerPermissions
{
    public const string LedgerAccountsCreate = "LEDGER_ACCOUNTS_CREATE";
    public const string LedgerAccountsRead = "LEDGER_ACCOUNTS_READ";
    public const string TransactionsTransferCreate = "TRANSACTIONS_TRANSFER_CREATE";
    public const string TransactionsRead = "TRANSACTIONS_READ";
    public const string TransactionsReverse = "TRANSACTIONS_REVERSE";
    public static IReadOnlyCollection<string> All { get; } = [LedgerAccountsCreate, LedgerAccountsRead, TransactionsTransferCreate, TransactionsRead, TransactionsReverse];
}
public sealed class PaymentLedgerPermissionContribution : IPermissionContribution
{
    public IReadOnlyCollection<string> Permissions => PaymentLedgerPermissions.All;
}
