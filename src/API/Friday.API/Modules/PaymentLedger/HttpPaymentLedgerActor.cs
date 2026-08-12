using System.Security.Claims;
using System.Diagnostics;
using Friday.Modules.PaymentLedger.Application.Actors;

namespace Friday.API.Modules.PaymentLedger;

public sealed class HttpPaymentLedgerActor(IHttpContextAccessor accessor) : IPaymentLedgerActor
{
    private HttpContext Context => accessor.HttpContext
        ?? throw new InvalidOperationException("PaymentLedger actor requires an active HTTP request.");

    public string UserId =>
        Context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Context.User.FindFirstValue("sub")
        ?? throw new InvalidOperationException(
            "Authenticated PaymentLedger actor identifier is missing."
        );

    public string TraceId => Activity.Current?.TraceId.ToString() ?? Context.TraceIdentifier;
}
