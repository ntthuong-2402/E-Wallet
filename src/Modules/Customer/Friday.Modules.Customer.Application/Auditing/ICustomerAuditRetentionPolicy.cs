namespace Friday.Modules.Customer.Application.Auditing;

public interface ICustomerAuditRetentionPolicy
{
    DateTime GetRetainUntilUtc(DateTime occurredOnUtc);
}
