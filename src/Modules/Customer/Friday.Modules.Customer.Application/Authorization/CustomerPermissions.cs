using Friday.BuildingBlocks.Application.Authorization;

namespace Friday.Modules.Customer.Application.Authorization;

public static class CustomerPermissions
{
    public const string Create = "CUSTOMERS_CREATE";
    public const string Read = "CUSTOMERS_READ";
    public const string PiiRead = "CUSTOMERS_PII_READ";
    public const string Update = "CUSTOMERS_UPDATE";
    public const string StatusChange = "CUSTOMERS_STATUS_CHANGE";
    public const string AuditRead = "CUSTOMERS_AUDIT_READ";
    public const string AccountLinkageRead = "CUSTOMERS_ACCOUNT_LINKAGE_READ";
    public const string AccountLinkageManage = "CUSTOMERS_ACCOUNT_LINKAGE_MANAGE";

    public static IReadOnlyCollection<string> All { get; } =
        [Create, Read, PiiRead, Update, StatusChange, AuditRead, AccountLinkageRead, AccountLinkageManage];
}

public sealed class CustomerPermissionContribution : IPermissionContribution
{
    public IReadOnlyCollection<string> Permissions => CustomerPermissions.All;
}
