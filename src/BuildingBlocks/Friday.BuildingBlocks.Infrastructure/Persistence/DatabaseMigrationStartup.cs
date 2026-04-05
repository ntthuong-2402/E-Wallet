using FluentMigrator.Runner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.BuildingBlocks.Infrastructure.Persistence;

public static class DatabaseMigrationStartup
{
    /// <summary>
    /// Applies EF Core schema migrations, then FluentMigrator data migrations. Skips when disabled, when the DbContext is not relational, or when FluentMigrator was not registered (in-memory database).
    /// </summary>
    public static async Task ApplyEfThenDataMigrationsAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !configuration.GetValue(
                $"{DatabaseOptions.SectionName}:{nameof(DatabaseOptions.ApplyMigrationsOnStartup)}",
                false
            )
        )
        {
            return;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        FridayDbContext db = scope.ServiceProvider.GetRequiredService<FridayDbContext>();
        if (!db.Database.IsRelational())
        {
            return;
        }

        await db.Database.MigrateAsync(cancellationToken);

        IMigrationRunner? runner = scope.ServiceProvider.GetService<IMigrationRunner>();
        runner?.MigrateUp();
    }
}
