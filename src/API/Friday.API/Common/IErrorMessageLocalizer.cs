namespace Friday.API.Common;

public interface IErrorMessageLocalizer
{
    Task<string> GetMessageAsync(
        string errorCode,
        string? language,
        string fallbackMessage,
        CancellationToken cancellationToken = default
    );
}
