using Friday.BuildingBlocks.Application.Abstractions;
using Friday.Modules.Customer.Application.Persistence;
using Friday.Modules.Customer.Application.Auditing;
using Friday.Modules.Customer.Application.Customers;
using Friday.Modules.Customer.Domain.Repositories;
using Friday.Modules.Customer.Infrastructure.Auditing;
using Friday.Modules.Customer.Infrastructure.Persistence;
using Friday.Modules.Customer.Infrastructure.Repositories;
using Friday.Modules.Customer.Infrastructure.Security;
using Friday.Modules.Customer.Infrastructure.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.Modules.Customer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        string? connectionString = configuration.GetConnectionString("CustomerDb");

        services.AddOptions<CustomerAuditRetentionOptions>()
            .Bind(configuration.GetSection(CustomerAuditRetentionOptions.SectionName))
            .Validate(
                value => value.RetentionDays > 0
                    && value.BatchSize is >= 1 and <= 10_000
                    && value.IntervalMinutes > 0,
                "Customer audit retention configuration is invalid."
            )
            .ValidateOnStart();

        services.AddDbContext<CustomerDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                string databaseName = configuration["Database:InMemoryDatabaseName"]
                    ?? "Friday.Shared";
                options.UseInMemoryDatabase($"{databaseName}.Customer");
                return;
            }

            options.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(CustomerDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable(
                        CustomerDbContext.MigrationHistoryTableName,
                        CustomerDbContext.SchemaName
                    );
                }
            );
        });

        services.AddScoped<ICustomerUnitOfWork, CustomerUnitOfWork>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerReadStore, CustomerReadStore>();
        services.AddScoped<ICustomerAuditRepository, CustomerAuditRepository>();
        services.AddSingleton<ICustomerCodeGenerator, SecureCustomerCodeGenerator>();
        services.AddSingleton<ICustomerAuditRetentionPolicy, CustomerAuditRetentionPolicy>();
        services.AddHostedService<CustomerAuditRetentionWorker>();
        services.AddHealthChecks()
            .AddCheck<CustomerDatabaseHealthCheck>("customer-database", tags: ["ready"]);
        services.AddKeyedScoped<IUnitOfWork>(
            CustomerPersistence.UnitOfWorkKey,
            (provider, _) => provider.GetRequiredService<ICustomerUnitOfWork>()
        );

        return services;
    }
}
