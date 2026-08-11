using Friday.Modules.Admin.Application.Authorization;
using Friday.Modules.Admin.Application.Security;
using Friday.BuildingBlocks.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.Modules.Admin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IPermissionContribution, AdminPermissionContribution>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IPasswordPolicy, PasswordPolicy>();
        return services;
    }
}
