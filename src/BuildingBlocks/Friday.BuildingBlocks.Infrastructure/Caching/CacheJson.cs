using System.Text.Json;

namespace Friday.BuildingBlocks.Infrastructure.Caching;

internal static class CacheJson
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web
    );
}
