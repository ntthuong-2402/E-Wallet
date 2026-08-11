using Friday.BuildingBlocks.Domain.Entities;

namespace Friday.Modules.Customer.Domain.Customers;

public sealed class Customer : AggregateRoot
{
    private Customer() { }

    public string CustomerCode { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public DateOnly? DateOfBirth { get; private set; }
    public CitizenDocumentType CitizenDocumentType { get; private set; }
    public string CitizenIssuingCountryCode { get; private set; } = string.Empty;
    public string CitizenDocumentNumber { get; private set; } = string.Empty;
    public string CitizenIdMasked { get; private set; } = string.Empty;
    public CustomerStatus Status { get; private set; }
    public DateTime OpenedOnUtc { get; private set; }
    public long Version { get; private set; }

    public static Customer Create(
        string customerCode,
        string fullName,
        DateOnly? dateOfBirth,
        CitizenDocument citizenDocument,
        DateTime openedOnUtc
    )
    {
        ArgumentNullException.ThrowIfNull(citizenDocument);
        if (string.IsNullOrWhiteSpace(customerCode))
        {
            throw new ArgumentException("Customer code is required.", nameof(customerCode));
        }

        if (openedOnUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Opened timestamp must be UTC.", nameof(openedOnUtc));
        }

        Customer customer = new()
        {
            CustomerCode = customerCode.Trim().ToUpperInvariant(),
            FullName = RequireFullName(fullName),
            DateOfBirth = dateOfBirth,
            Status = CustomerStatus.Active,
            OpenedOnUtc = openedOnUtc,
        };
        customer.SetCitizenDocument(citizenDocument);
        return customer;
    }

    public void UpdateProfile(
        string fullName,
        DateOnly? dateOfBirth,
        CitizenDocument citizenDocument
    )
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(citizenDocument);
        FullName = RequireFullName(fullName);
        DateOfBirth = dateOfBirth;
        SetCitizenDocument(citizenDocument);
        Version++;
        Touch();
    }

    public void Suspend(string reason) => ChangeStatus(CustomerStatus.Suspended, reason);

    public void Reactivate(string reason) => ChangeStatus(CustomerStatus.Active, reason);

    public void Close(string reason) => ChangeStatus(CustomerStatus.Closed, reason);

    private void ChangeStatus(CustomerStatus target, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required for status changes.", nameof(reason));
        }

        if (Status == CustomerStatus.Closed)
        {
            throw new InvalidOperationException("A closed Customer cannot change status.");
        }

        bool allowed = (Status, target) switch
        {
            (CustomerStatus.Active, CustomerStatus.Suspended) => true,
            (CustomerStatus.Active, CustomerStatus.Closed) => true,
            (CustomerStatus.Suspended, CustomerStatus.Active) => true,
            (CustomerStatus.Suspended, CustomerStatus.Closed) => true,
            _ => false,
        };

        if (!allowed)
        {
            throw new InvalidOperationException($"Cannot change Customer status from {Status} to {target}.");
        }

        Status = target;
        Version++;
        Touch();
    }

    private void SetCitizenDocument(CitizenDocument document)
    {
        CitizenDocumentType = document.DocumentType;
        CitizenIssuingCountryCode = document.IssuingCountryCode;
        CitizenDocumentNumber = document.Number;
        CitizenIdMasked = document.MaskedValue;
    }

    private void EnsureOpen()
    {
        if (Status == CustomerStatus.Closed)
        {
            throw new InvalidOperationException("A closed Customer cannot be updated.");
        }
    }

    private static string RequireFullName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Full name is required.", nameof(value));
        if (value.Any(char.IsControl))
            throw new ArgumentException("Full name cannot contain control characters.", nameof(value));

        string normalized = string.Join(' ', value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        ));
        return normalized.Length > 200
            ? throw new ArgumentException("Full name cannot exceed 200 characters.", nameof(value))
            : normalized;
    }
}
