using Microsoft.Extensions.DependencyInjection;
using Friday.BuildingBlocks.Application.Authorization;
using Friday.Modules.Customer.Application.Authorization;

namespace Friday.Modules.Customer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomerApplication(this IServiceCollection services)
    {
        services.AddSingleton<IPermissionContribution, CustomerPermissionContribution>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
