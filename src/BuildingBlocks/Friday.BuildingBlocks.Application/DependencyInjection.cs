using Microsoft.Extensions.DependencyInjection;

namespace Friday.BuildingBlocks.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBuildingBlocksApplication(this IServiceCollection services)
    {
        return services;
    }
}
