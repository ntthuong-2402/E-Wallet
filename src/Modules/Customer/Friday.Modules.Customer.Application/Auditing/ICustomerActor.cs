namespace Friday.Modules.Customer.Application.Auditing;

public interface ICustomerActor
{
    string UserId { get; }
    string TraceId { get; }
}
