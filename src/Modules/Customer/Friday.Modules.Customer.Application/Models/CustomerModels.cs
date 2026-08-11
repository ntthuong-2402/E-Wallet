using Friday.Modules.Customer.Domain.Customers;
using CustomerAggregate = Friday.Modules.Customer.Domain.Customers.Customer;

namespace Friday.Modules.Customer.Application.Models;

public sealed record CustomerDetailDto(
    int Id,
    string CustomerCode,
    string DisplayName,
    CitizenDocumentType CitizenDocumentType,
    string CitizenIssuingCountryCode,
    string CitizenIdMasked,
    CustomerStatus Status,
    DateTime OpenedOnUtc,
    long Version,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc
)
{
    public static CustomerDetailDto FromCustomer(CustomerAggregate customer) => new(
        customer.Id,
        customer.CustomerCode,
        MaskName(customer.FullName),
        customer.CitizenDocumentType,
        customer.CitizenIssuingCountryCode,
        customer.CitizenIdMasked,
        customer.Status,
        customer.OpenedOnUtc,
        customer.Version,
        customer.CreatedOnUtc,
        customer.UpdatedOnUtc
    );

    private static string MaskName(string value) =>
        value.Length == 0 ? "*" : $"{value[0]}***";
}

public sealed record CustomerListItemDto(
    int Id,
    string CustomerCode,
    string DisplayName,
    CitizenDocumentType CitizenDocumentType,
    string CitizenIdMasked,
    CustomerStatus Status,
    DateTime OpenedOnUtc,
    long Version
);

public sealed record CustomerAuditDto(
    long EventId,
    string EventType,
    string ChangedFieldsJson,
    string? FromStatus,
    string? ToStatus,
    string? Reason,
    string Outcome,
    string ActorUserId,
    string TraceId,
    DateTime OccurredOnUtc
);

public sealed record CustomerSearchCriteria(
    string? CustomerCode,
    CustomerStatus? Status,
    DateTime? OpenedFrom,
    DateTime? OpenedTo,
    int Page,
    int PageSize,
    string SortBy,
    bool Descending
);

public sealed record CustomerSearchPage(
    IReadOnlyList<CustomerListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
