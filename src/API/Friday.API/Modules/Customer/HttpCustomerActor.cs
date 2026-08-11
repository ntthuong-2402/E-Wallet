using System.Diagnostics;
using System.Security.Claims;
using Friday.Modules.Customer.Application.Auditing;

namespace Friday.API.Modules.Customer;

public sealed class HttpCustomerActor(IHttpContextAccessor accessor) : ICustomerActor
{
    private HttpContext Context => accessor.HttpContext
        ?? throw new InvalidOperationException("Customer actor requires an active HTTP request.");

    public string UserId =>
        Context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Context.User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Authenticated Customer actor identifier is missing.");

    public string TraceId => Activity.Current?.TraceId.ToString() ?? Context.TraceIdentifier;
}
