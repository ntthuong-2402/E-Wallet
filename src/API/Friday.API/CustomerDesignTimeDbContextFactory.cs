using Friday.Modules.Customer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Friday.API;

/// <summary>
/// Design-time factory for Customer-owned migrations. Set
/// <c>FRIDAY_CUSTOMER_DESIGN_TIME_PG</c> to override the local PostgreSQL
/// connection string.
/// </summary>
public sealed class CustomerDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<CustomerDbContext>
{
    public CustomerDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<CustomerDbContext> options = new();
        string connectionString =
            Environment.GetEnvironmentVariable("FRIDAY_CUSTOMER_DESIGN_TIME_PG")
            ?? "Host=127.0.0.1;Port=5432;Database=friday;Username=friday;Password=friday";

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

        return new CustomerDbContext(options.Options);
    }
}
