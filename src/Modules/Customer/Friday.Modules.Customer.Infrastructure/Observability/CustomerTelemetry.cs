using System.Diagnostics.Metrics;

namespace Friday.Modules.Customer.Infrastructure.Observability;

public static class CustomerTelemetry
{
    public const string MeterName = "Friday.Customer";
    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> RetentionPurged =
        Meter.CreateCounter<long>("customer.audit.retention.purged");
    public static readonly Counter<long> RetentionFailures =
        Meter.CreateCounter<long>("customer.audit.retention.failures");
    public static readonly Histogram<long> RetentionCandidates =
        Meter.CreateHistogram<long>("customer.audit.retention.candidates");
    public static readonly Histogram<double> RetentionDuration =
        Meter.CreateHistogram<double>("customer.audit.retention.duration", "ms");
    public static readonly Counter<long> DocumentConflicts =
        Meter.CreateCounter<long>("customer.document.conflicts");
    public static readonly Counter<long> ConcurrencyConflicts =
        Meter.CreateCounter<long>("customer.concurrency.conflicts");
    public static readonly Counter<long> AuditWriteFailures =
        Meter.CreateCounter<long>("customer.audit.write.failures");
}
