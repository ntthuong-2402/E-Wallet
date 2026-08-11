using System.Diagnostics;
using Friday.Modules.Customer.Application.Auditing;
using Friday.Modules.Customer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Friday.Modules.Customer.Infrastructure.Observability;

namespace Friday.Modules.Customer.Infrastructure.Auditing;

public sealed class CustomerAuditRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CustomerAuditRetentionOptions> options,
    TimeProvider timeProvider,
    IConfiguration configuration,
    ILogger<CustomerAuditRetentionWorker> logger
) : BackgroundService
{
    private readonly CustomerAuditRetentionOptions settings = Validate(options.Value);
    private readonly string? retentionConnectionString =
        configuration.GetConnectionString("CustomerRetentionDb");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.WorkerEnabled)
        {
            logger.LogInformation("Customer audit retention worker is disabled.");
            return;
        }

        using PeriodicTimer timer = new(TimeSpan.FromMinutes(settings.IntervalMinutes), timeProvider);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            CustomerDbContext db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
            DateTime cutoff = timeProvider.GetUtcNow().UtcDateTime;

            if (settings.DryRun)
            {
                int candidates = await db.CustomerChangeAudits.AsNoTracking()
                    .CountAsync(x => x.RetainUntilUtc <= cutoff, cancellationToken);
                CustomerTelemetry.RetentionCandidates.Record(candidates);
                logger.LogInformation("Customer audit retention dry-run found {CandidateCount} expired records.", candidates);
                return;
            }

            if (!db.Database.IsRelational())
            {
                logger.LogWarning("Customer audit retention purge requires a relational database provider.");
                return;
            }

            if (string.IsNullOrWhiteSpace(retentionConnectionString))
                throw new InvalidOperationException(
                    "CustomerRetentionDb is required when retention purge is enabled."
                );

            await using NpgsqlConnection connection = new(retentionConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using NpgsqlCommand command = new(
                "SELECT customer.purge_expired_customer_audits(@batch_size)",
                connection
            );
            command.Parameters.AddWithValue("batch_size", settings.BatchSize);
            int purged = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            CustomerTelemetry.RetentionPurged.Add(purged);
            logger.LogInformation("Customer audit retention purged {PurgedCount} expired records.", purged);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            CustomerTelemetry.RetentionFailures.Add(1);
            logger.LogError(ex, "Customer audit retention run failed.");
        }
        finally
        {
            CustomerTelemetry.RetentionDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static CustomerAuditRetentionOptions Validate(CustomerAuditRetentionOptions value)
    {
        if (value.RetentionDays <= 0 || value.BatchSize is < 1 or > 10_000 || value.IntervalMinutes <= 0)
            throw new InvalidOperationException("Customer audit retention configuration is invalid.");
        return value;
    }
}
