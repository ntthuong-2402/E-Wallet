using Microsoft.Extensions.DependencyInjection;

namespace Friday.Modules.Sample.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSampleApplication(this IServiceCollection services)
    {
        return services;
    }
}
