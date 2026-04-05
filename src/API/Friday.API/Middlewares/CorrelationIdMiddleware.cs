using System.Diagnostics;
using Serilog.Context;

namespace Friday.API.Middlewares;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId =
            context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Activity.Current?.TraceId.ToString()
            ?? context.TraceIdentifier;

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
