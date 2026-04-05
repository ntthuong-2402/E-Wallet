namespace Friday.BuildingBlocks.Application.Exceptions;

public class FridayException : Exception
{
    public FridayException(string errorCode, string message, int statusCode = 400)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public string ErrorCode { get; }

    public int StatusCode { get; }
}
