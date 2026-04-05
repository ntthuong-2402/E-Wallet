using System.Text.Json;
using Friday.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Caching.Distributed;

namespace Friday.BuildingBlocks.Infrastructure.Caching;

public sealed class RedisCacheService(IDistributedCache distributedCache) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await distributedCache
            .GetAsync(key, cancellationToken)
            .ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(bytes, CacheJson.SerializerOptions);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpirationRelativeToNow = null,
        CancellationToken cancellationToken = default
    )
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, CacheJson.SerializerOptions);
        DistributedCacheEntryOptions options = new();
        if (absoluteExpirationRelativeToNow is { } ttl)
        {
            options.AbsoluteExpirationRelativeToNow = ttl;
        }

        await distributedCache
            .SetAsync(key, bytes, options, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        distributedCache.RemoveAsync(key, cancellationToken);
}
