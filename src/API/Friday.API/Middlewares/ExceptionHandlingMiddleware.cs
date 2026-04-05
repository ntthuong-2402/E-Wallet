using System.Diagnostics;
using Friday.API.Common;
using Friday.BuildingBlocks.Application.Errors;
using Friday.BuildingBlocks.Application.Exceptions;

namespace Friday.API.Middlewares;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger
)
{
    public async Task InvokeAsync(HttpContext context, IErrorMessageLocalizer localizer)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            (int statusCode, string code, string message) = Map(exception);
            string language = context.Request.Headers.AcceptLanguage.ToString();
            string localizedMessage = await localizer.GetMessageAsync(
                code,
                language,
                message,
                context.RequestAborted
            );
            string traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

            logger.LogError(
                exception,
                "Unhandled exception: {Code} - {Message}. TraceId={TraceId}",
                code,
                exception.Message,
                traceId
            );

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                ApiResponse.Fail(code, localizedMessage, traceId)
            );
        }
    }

    private static (int statusCode, string code, string message) Map(Exception exception)
    {
        return exception switch
        {
            FridayException fridayException => (
                fridayException.StatusCode,
                fridayException.ErrorCode,
                fridayException.Message
            ),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                ErrorCodes.Common.NotFound,
                exception.Message
            ),
            ArgumentException => (
                StatusCodes.Status400BadRequest,
                ErrorCodes.Common.BadRequest,
                exception.Message
            ),
            InvalidOperationException => (
                StatusCodes.Status400BadRequest,
                ErrorCodes.Common.BadRequest,
                exception.Message
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                ErrorCodes.Common.InternalServerError,
                "An unexpected error occurred."
            ),
        };
    }
}
