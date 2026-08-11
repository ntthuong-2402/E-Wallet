using Friday.Modules.Customer.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Friday.Modules.Customer.Application.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

namespace Friday.Modules.Customer.Infrastructure.Health;

public sealed class CustomerDatabaseHealthCheck(
    CustomerDbContext dbContext,
    IOptions<CustomerAuditRetentionOptions> retentionOptions,
    IConfiguration configuration
) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Customer database is unavailable.");
            CustomerAuditRetentionOptions retention = retentionOptions.Value;
            if (retention.WorkerEnabled && !retention.DryRun
                && string.IsNullOrWhiteSpace(
                    configuration.GetConnectionString("CustomerRetentionDb")))
                return HealthCheckResult.Unhealthy(
                    "Customer retention database connection is not configured."
                );
            if (!dbContext.Database.IsRelational())
                return HealthCheckResult.Healthy();

            if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                return HealthCheckResult.Unhealthy("Customer database migrations are pending.");

            if (retention.WorkerEnabled && !retention.DryRun)
            {
                bool artifactsReady = await dbContext.Database.SqlQueryRaw<bool>("""
                    SELECT (
                        to_regprocedure('customer.purge_expired_customer_audits(integer)') IS NOT NULL
                        AND EXISTS (
                            SELECT 1 FROM pg_trigger
                            WHERE tgname = 'customer_audit_append_only' AND NOT tgisinternal
                        )
                    ) AS "Value"
                    """).SingleAsync(cancellationToken);
                if (!artifactsReady)
                    return HealthCheckResult.Unhealthy("Customer retention database artifacts are unavailable.");
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Customer database readiness check failed.", exception);
        }
    }
}
