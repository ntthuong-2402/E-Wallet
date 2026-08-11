using Friday.Modules.Customer.Application.Auditing;
using Microsoft.Extensions.Options;

namespace Friday.Modules.Customer.Infrastructure.Auditing;

public sealed class CustomerAuditRetentionPolicy(IOptions<CustomerAuditRetentionOptions> options)
    : ICustomerAuditRetentionPolicy
{
    private readonly int _retentionDays = options.Value.RetentionDays > 0
        ? options.Value.RetentionDays
        : throw new InvalidOperationException("Customer audit retention must be greater than zero days.");

    public DateTime GetRetainUntilUtc(DateTime occurredOnUtc)
    {
        if (occurredOnUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Audit timestamp must be UTC.", nameof(occurredOnUtc));

        return occurredOnUtc.AddDays(_retentionDays);
    }
}
