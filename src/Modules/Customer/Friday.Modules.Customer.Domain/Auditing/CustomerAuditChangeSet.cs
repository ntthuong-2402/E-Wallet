using System.Text.Json;
using Friday.Modules.Customer.Domain.Customers;
using CustomerAggregate = Friday.Modules.Customer.Domain.Customers.Customer;

namespace Friday.Modules.Customer.Domain.Auditing;

public sealed class CustomerAuditChangeSet
{
    private CustomerAuditChangeSet(string json) => Json = json;

    public string Json { get; }

    public static CustomerAuditChangeSet Created(CustomerAggregate customer) => FromFields(
        new Dictionary<string, object?>
        {
            [nameof(CustomerAggregate.CustomerCode)] = customer.CustomerCode,
            [nameof(CustomerAggregate.FullName)] = MaskName(customer.FullName),
            [nameof(CustomerAggregate.DateOfBirth)] = MaskDate(customer.DateOfBirth),
            ["CitizenId"] = customer.CitizenIdMasked,
            [nameof(CustomerAggregate.Status)] = customer.Status.ToString(),
            [nameof(CustomerAggregate.OpenedOnUtc)] = customer.OpenedOnUtc,
        }
    );

    public static CustomerAuditChangeSet ProfileUpdated(
        string previousFullName,
        DateOnly? previousDateOfBirth,
        string previousCitizenIdMasked,
        CustomerAggregate customer
    ) => FromFields(
        new Dictionary<string, object?>
        {
            [nameof(CustomerAggregate.FullName)] = new
            {
                Before = MaskName(previousFullName),
                After = MaskName(customer.FullName),
            },
            [nameof(CustomerAggregate.DateOfBirth)] = new
            {
                Before = MaskDate(previousDateOfBirth),
                After = MaskDate(customer.DateOfBirth),
            },
            ["CitizenId"] = new
            {
                Before = previousCitizenIdMasked,
                After = customer.CitizenIdMasked,
            },
        }
    );

    public static CustomerAuditChangeSet StatusChanged(CustomerStatus from, CustomerStatus to) =>
        FromFields(new Dictionary<string, object?>
        {
            [nameof(CustomerAggregate.Status)] = new { Before = from.ToString(), After = to.ToString() },
        });

    private static CustomerAuditChangeSet FromFields(IReadOnlyDictionary<string, object?> fields) =>
        new(JsonSerializer.Serialize(fields));

    private static string MaskName(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Length <= 1 ? "*" : $"{trimmed[0]}{new string('*', trimmed.Length - 1)}";
    }

    private static string? MaskDate(DateOnly? value) => value.HasValue ? "****-**-**" : null;
}
