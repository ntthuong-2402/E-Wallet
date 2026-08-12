namespace Friday.Modules.Customer.Application.Errors;

public static class CustomerErrorCodes
{
    public const string NotFound = "CUSTOMER_NOT_FOUND";
    public const string CodeConflict = "CUSTOMER_CODE_CONFLICT";
    public const string DocumentConflict = "CUSTOMER_DOCUMENT_CONFLICT";
    public const string InvalidStatusTransition = "CUSTOMER_INVALID_STATUS_TRANSITION";
    public const string ConcurrencyConflict = "CUSTOMER_CONCURRENCY_CONFLICT";
    public const string Closed = "CUSTOMER_CLOSED";
    public const string RefIdConflict = "CUSTOMER_REF_ID_CONFLICT";
    public const string AccountNotFound = "CUSTOMER_ACCOUNT_NOT_FOUND";
    public const string AccountAlreadyLinked = "CUSTOMER_ACCOUNT_ALREADY_LINKED";
    public const string AccountLinkageNotFound = "CUSTOMER_ACCOUNT_LINKAGE_NOT_FOUND";
}
