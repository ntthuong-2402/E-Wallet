namespace Friday.API.Common;

public sealed record ApiResponse<T>(string Code, string Message, T? Data, string? TraceId)
{
    public static ApiResponse<T> Success(
        T data,
        string message = "Success",
        string code = "SUCCESS",
        string? traceId = null
    )
    {
        return new ApiResponse<T>(code, message, data, traceId);
    }
}

public sealed record ApiResponse(string Code, string Message, object? Data, string? TraceId)
{
    public static ApiResponse Success(
        object? data,
        string message = "Success",
        string code = "SUCCESS",
        string? traceId = null
    )
    {
        return new ApiResponse(code, message, data, traceId);
    }

    public static ApiResponse Fail(string code, string message, string? traceId = null)
    {
        return new ApiResponse(code, message, null, traceId);
    }
}
