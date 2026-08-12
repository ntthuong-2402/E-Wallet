namespace Friday.Modules.PaymentLedger.Application.Actors;

public interface IPaymentLedgerActor
{
    string UserId { get; }
    string TraceId { get; }
}
