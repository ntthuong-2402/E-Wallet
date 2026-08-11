using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.Modules.Customer.Infrastructure.Persistence;

public static class CustomerMigrationStartup
{
    public static async Task ApplyCustomerMigrationsAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default
    )
    {
        if (!configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
        {
            return;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        CustomerDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<CustomerDbContext>();

        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }
}
