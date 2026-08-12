using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.Modules.PaymentLedger.Infrastructure.Persistence;

public static class PaymentLedgerMigrationStartup
{
    public static async Task ApplyPaymentLedgerMigrationsAsync(this IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Database:ApplyMigrationsOnStartup", false)) return;
        string? migrationConnection = configuration.GetConnectionString(
            "PaymentLedgerMigrationDb"
        );
        if (!string.IsNullOrWhiteSpace(migrationConnection))
        {
            DbContextOptionsBuilder<PaymentLedgerDbContext> options = new();
            options.UseNpgsql(migrationConnection, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(PaymentLedgerDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable(
                    PaymentLedgerDbContext.MigrationHistoryTableName,
                    PaymentLedgerDbContext.SchemaName
                );
            });
            await using PaymentLedgerDbContext migrationDb = new(options.Options);
            await migrationDb.Database.MigrateAsync(cancellationToken);
            return;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        PaymentLedgerDbContext db = scope.ServiceProvider.GetRequiredService<PaymentLedgerDbContext>();
        if (db.Database.IsRelational()) await db.Database.MigrateAsync(cancellationToken);
    }
}
