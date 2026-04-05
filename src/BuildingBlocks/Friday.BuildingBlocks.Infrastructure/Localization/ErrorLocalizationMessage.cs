namespace Friday.BuildingBlocks.Infrastructure.Localization;

public sealed class ErrorLocalizationMessage
{
    public int Id { get; set; }
    public string Module { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime UpdatedOnUtc { get; set; } = DateTime.UtcNow;
}
