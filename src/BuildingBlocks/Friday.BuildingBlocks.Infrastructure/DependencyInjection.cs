using FluentMigrator.Runner;
using Friday.BuildingBlocks.Application.Abstractions;
using Friday.BuildingBlocks.Application.Localization;
using Friday.BuildingBlocks.Infrastructure.Caching;
using Friday.BuildingBlocks.Infrastructure.DataMigrations;
using Friday.BuildingBlocks.Infrastructure.Localization;
using Friday.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBuildingBlocksInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));

        AddApplicationCache(services, configuration);

        DatabaseOptions dbSettings =
            configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions();

        string? connectionString = configuration.GetConnectionString("FridayDb");

        string linq2DbConnection =
            connectionString
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=FridayDb;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddScoped<ILinqToDbConnectionFactory>(_ => new LinqToDbConnectionFactory(
            linq2DbConnection,
            dbSettings.Provider
        ));

        services.AddDbContext<FridayDbContext>(options =>
            RelationalDbContextConfigurer.Configure(options, connectionString, dbSettings.Provider)
        );

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services
                .AddFluentMigratorCore()
                .ConfigureRunner(rb =>
                {
                    switch (dbSettings.Provider)
                    {
                        case RelationalDatabaseProvider.SqlServer:
                            rb.AddSqlServer();
                            break;
                        case RelationalDatabaseProvider.PostgreSql:
                            rb.AddPostgres15_0();
                            break;
                        case RelationalDatabaseProvider.MySql:
                            rb.AddMySql8();
                            break;
                        case RelationalDatabaseProvider.Oracle:
                            rb.AddOracleManaged();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(dbSettings.Provider),
                                dbSettings.Provider,
                                "Unknown database provider."
                            );
                    }

                    rb.WithGlobalConnectionString(connectionString)
                        .ScanIn(typeof(DataMigrationAssemblyMarker).Assembly)
                        .For.Migrations();
                })
                .AddLogging(lb => lb.AddFluentMigratorConsole());
        }

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<IErrorLocalizationStore, EfErrorLocalizationStore>();

        return services;
    }

    private static void AddApplicationCache(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddMemoryCache();

        CacheOptions cacheOptions =
            configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>()
            ?? new CacheOptions();

        string? redisConnection = !string.IsNullOrWhiteSpace(cacheOptions.RedisConnectionString)
            ? cacheOptions.RedisConnectionString
            : configuration.GetConnectionString("Redis");

        if (cacheOptions.UseRedis && !string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;

                options.InstanceName = string.IsNullOrWhiteSpace(cacheOptions.RedisInstanceName)
                    ? "Friday:"
                    : cacheOptions.RedisInstanceName;
            });

            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }
    }
}
