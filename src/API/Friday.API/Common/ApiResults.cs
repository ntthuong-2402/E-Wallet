using System.Diagnostics;
using Friday.BuildingBlocks.Application.Errors;

namespace Friday.API.Common;

public static class ApiResults
{
    public static IResult Ok<T>(
        HttpContext httpContext,
        T data,
        string message = "Success",
        string code = ErrorCodes.Common.Success
    )
    {
        string traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        return Results.Ok(ApiResponse<T>.Success(data, message, code, traceId));
    }
}
