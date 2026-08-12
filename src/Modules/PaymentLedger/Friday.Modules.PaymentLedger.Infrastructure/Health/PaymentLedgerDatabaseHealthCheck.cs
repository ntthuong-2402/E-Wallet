using Friday.Modules.PaymentLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Friday.Modules.PaymentLedger.Infrastructure.Health;

public sealed class PaymentLedgerDatabaseHealthCheck(PaymentLedgerDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try { if (!await db.Database.CanConnectAsync(cancellationToken)) return HealthCheckResult.Unhealthy("PaymentLedger database is unavailable."); if (db.Database.IsRelational() && (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any()) return HealthCheckResult.Unhealthy("PaymentLedger database migrations are pending."); return HealthCheckResult.Healthy(); }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("PaymentLedger database readiness check failed.", ex); }
    }
}
