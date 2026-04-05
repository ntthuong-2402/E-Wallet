using Friday.Modules.Admin.Application.Configuration;
using Friday.Modules.Admin.Application.Security;
using Friday.Modules.Admin.Domain.Repositories;
using Friday.Modules.Admin.Domain.Security;
using Friday.Modules.Admin.Infrastructure.Repositories;
using Friday.Modules.Admin.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.Modules.Admin.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IPasswordHasher<CredentialUser>, PasswordHasher<CredentialUser>>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRightRepository, RightRepository>();

        return services;
    }
}
