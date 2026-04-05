using Microsoft.Extensions.DependencyInjection;

namespace Friday.Modules.Admin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        return services;
    }
}
