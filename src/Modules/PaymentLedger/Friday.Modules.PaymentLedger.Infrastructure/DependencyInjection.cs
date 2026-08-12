using Friday.BuildingBlocks.Application.Abstractions;
using Friday.Modules.PaymentLedger.Application.Persistence;
using Friday.Modules.PaymentLedger.Application.Queries;
using Friday.Modules.PaymentLedger.Domain.Repositories;
using Friday.Modules.PaymentLedger.Infrastructure.Health;
using Friday.Modules.PaymentLedger.Infrastructure.Persistence;
using Friday.Modules.PaymentLedger.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.Modules.PaymentLedger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentLedgerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("PaymentLedgerDb");
        services.AddDbContext<PaymentLedgerDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                string name = configuration["Database:InMemoryDatabaseName"] ?? "Friday.Shared";
                options.UseInMemoryDatabase($"{name}.PaymentLedger");
            }
            else
            {
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(PaymentLedgerDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable(PaymentLedgerDbContext.MigrationHistoryTableName, PaymentLedgerDbContext.SchemaName);
                });
            }
        });
        services.AddScoped<IPaymentLedgerUnitOfWork, PaymentLedgerUnitOfWork>();
        services.AddKeyedScoped<IUnitOfWork>(PaymentLedgerPersistence.UnitOfWorkKey, (provider, _) => provider.GetRequiredService<IPaymentLedgerUnitOfWork>());
        services.AddScoped<ILedgerAccountRepository, LedgerAccountRepository>();
        services.AddScoped<IFinancialTransactionRepository, FinancialTransactionRepository>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        services.AddScoped<IFinancialAuditRepository, FinancialAuditRepository>();
        services.AddScoped<IPaymentLedgerReadStore, PaymentLedgerReadStore>();
        services.AddHealthChecks().AddCheck<PaymentLedgerDatabaseHealthCheck>("payment-ledger-database", tags: ["ready"]);
        return services;
    }
}
