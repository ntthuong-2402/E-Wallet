using Microsoft.OpenApi.Models;

namespace Friday.API.Configuration;

public static class FridaySwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddFridaySwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(
                "v1",
                new OpenApiInfo
                {
                    Title = "Friday API",
                    Version = "v1",
                    Description = "Friday modular monolith — REST endpoints.",
                }
            );

            options.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    Description = "JWT: nhập token (Swagger sẽ gửi header `Authorization: Bearer {token}`).",
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                }
            );

            options.AddSecurityRequirement(
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer",
                            },
                        },
                        Array.Empty<string>()
                    }
                }
            );
        });

        return services;
    }
}
