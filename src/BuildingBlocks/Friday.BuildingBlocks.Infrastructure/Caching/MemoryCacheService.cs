using System.Text.Json;
using Friday.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Friday.BuildingBlocks.Infrastructure.Caching;

public sealed class MemoryCacheService(IMemoryCache memoryCache) : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!memoryCache.TryGetValue(key, out byte[]? bytes))
        {
            return Task.FromResult<T?>(default);
        }

        return Task.FromResult(JsonSerializer.Deserialize<T>(bytes, CacheJson.SerializerOptions));
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpirationRelativeToNow = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, CacheJson.SerializerOptions);
        MemoryCacheEntryOptions options = new();
        if (absoluteExpirationRelativeToNow is { } ttl)
        {
            options.AbsoluteExpirationRelativeToNow = ttl;
        }

        memoryCache.Set(key, bytes, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        memoryCache.Remove(key);
        return Task.CompletedTask;
    }
}
