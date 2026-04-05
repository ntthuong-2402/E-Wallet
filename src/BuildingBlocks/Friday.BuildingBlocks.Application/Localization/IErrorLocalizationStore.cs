namespace Friday.BuildingBlocks.Application.Localization;

public interface IErrorLocalizationStore
{
    Task<string?> GetMessageAsync(
        string module,
        string errorCode,
        string language,
        CancellationToken cancellationToken = default
    );
}
