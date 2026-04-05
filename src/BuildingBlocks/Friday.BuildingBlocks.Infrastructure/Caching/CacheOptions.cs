namespace Friday.BuildingBlocks.Infrastructure.Caching;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>When true and a Redis connection is configured, <see cref="RedisCacheService"/> is used.</summary>
    public bool UseRedis { get; set; }

    /// <summary>
    /// StackExchange.Redis connection string (e.g. <c>localhost:6379</c>). If empty, <c>ConnectionStrings:Redis</c> is used.
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>Optional key prefix (instance name) for Redis; default <c>Friday:</c>.</summary>
    public string? RedisInstanceName { get; set; }
}
