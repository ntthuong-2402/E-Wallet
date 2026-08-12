using Friday.Modules.PaymentLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Friday.API;

public sealed class PaymentLedgerDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<PaymentLedgerDbContext>
{
    public PaymentLedgerDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<PaymentLedgerDbContext> options = new();
        string connectionString = Environment.GetEnvironmentVariable(
            "FRIDAY_PAYMENT_LEDGER_DESIGN_TIME_PG"
        ) ?? throw new InvalidOperationException(
            "Set FRIDAY_PAYMENT_LEDGER_DESIGN_TIME_PG before running PaymentLedger EF commands."
        );

        options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(PaymentLedgerDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable(
                    PaymentLedgerDbContext.MigrationHistoryTableName,
                    PaymentLedgerDbContext.SchemaName
                );
            }
        );

        return new PaymentLedgerDbContext(options.Options);
    }
}
