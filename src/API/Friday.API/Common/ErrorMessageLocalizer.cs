using Friday.BuildingBlocks.Application.Localization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Friday.API.Common;

public sealed class ErrorMessageLocalizer(
    IMemoryCache memoryCache,
    IErrorLocalizationStore store,
    IOptions<LocalizationOptions> options
) : IErrorMessageLocalizer
{
    public async Task<string> GetMessageAsync(
        string errorCode,
        string? language,
        string fallbackMessage,
        CancellationToken cancellationToken = default
    )
    {
        string module = ResolveModule(errorCode);
        string normalized = ResolveLanguage(language);
        string[] candidates = normalized.Contains('-')
            ? [normalized, normalized.Split('-')[0], "en"]
            : [normalized, "en"];

        foreach (string candidate in candidates.Distinct())
        {
            string cacheKey = $"loc:{module}:{errorCode}:{candidate}";
            if (!memoryCache.TryGetValue(cacheKey, out string? message))
            {
                message = await store.GetMessageAsync(
                    module,
                    errorCode,
                    candidate,
                    cancellationToken
                );
                if (!string.IsNullOrWhiteSpace(message))
                {
                    memoryCache.Set(
                        cacheKey,
                        message,
                        TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheMinutes))
                    );
                }
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }
        }

        return fallbackMessage;
    }

    private static string ResolveModule(string errorCode)
    {
        int first = errorCode.IndexOf('_');
        if (first <= 0)
        {
            return "common";
        }

        return errorCode[..first].ToLowerInvariant();
    }

    private static string ResolveLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "en";
        }

        return language.Split(',', ';')[0].Trim().ToLowerInvariant();
    }
}
