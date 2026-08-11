namespace Friday.Modules.Customer.Application.Auditing;

public sealed class CustomerAuditRetentionOptions
{
    public const string SectionName = "Customer:AuditRetention";
    public int RetentionDays { get; set; } = 365;
    public bool WorkerEnabled { get; set; }
    public bool DryRun { get; set; } = true;
    public int BatchSize { get; set; } = 500;
    public int IntervalMinutes { get; set; } = 60;
}
