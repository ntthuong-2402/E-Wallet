namespace Friday.Modules.PaymentLedger.Application.Errors;

public static class PaymentLedgerErrorCodes
{
    public const string AccountNotFound = "LEDGER_ACCOUNT_NOT_FOUND";
    public const string AccountRefConflict = "LEDGER_ACCOUNT_REF_CONFLICT";
    public const string AccountInactive = "LEDGER_ACCOUNT_INACTIVE";
    public const string InvalidTransfer = "LEDGER_INVALID_TRANSFER";
    public const string InsufficientFunds = "LEDGER_INSUFFICIENT_FUNDS";
    public const string TransactionNotFound = "LEDGER_TRANSACTION_NOT_FOUND";
    public const string InvalidReversal = "LEDGER_INVALID_REVERSAL";
    public const string RefIdConflict = "LEDGER_REF_ID_CONFLICT";
    public const string ConcurrencyConflict = "LEDGER_CONCURRENCY_CONFLICT";
}
