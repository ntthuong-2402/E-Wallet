using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Customer.Application.Auditing;
using Friday.Modules.Customer.Application.Customers;
using Friday.Modules.Customer.Application.Errors;
using Friday.Modules.Customer.Application.Models;
using Friday.Modules.Customer.Application.Persistence;
using Friday.Modules.Customer.Domain.Auditing;
using Friday.Modules.Customer.Domain.Customers;
using Friday.Modules.Customer.Domain.Repositories;
using LinKit.Core.Cqrs;
using CustomerAggregate = Friday.Modules.Customer.Domain.Customers.Customer;

namespace Friday.Modules.Customer.Application.Features.Customers;

public sealed record UpdateCustomerProfileCommand(
    int CustomerId,
    long ExpectedVersion,
    string FullName,
    DateOnly? DateOfBirth,
    CitizenDocumentType? DocumentType,
    string? IssuingCountryCode,
    string? CitizenDocumentNumber
) : ICustomerCommand<CustomerDetailDto>;

public sealed class UpdateCustomerProfileHandler(
    ICustomerRepository customers,
    ICustomerAuditRepository audits,
    ICustomerAuditRetentionPolicy retention,
    ICustomerActor actor,
    TimeProvider timeProvider
) : ICommandHandler<UpdateCustomerProfileCommand, CustomerDetailDto>
{
    public async Task<CustomerDetailDto> HandleAsync(
        UpdateCustomerProfileCommand request,
        CancellationToken cancellationToken
    )
    {
        CustomerAggregate customer = await customers.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new FridayException(CustomerErrorCodes.NotFound, "Customer was not found.", 404);
        EnsureExpectedVersion(customer, request.ExpectedVersion);

        CitizenDocument document = ResolveDocument(request, customer);
        bool documentChanged = document.Number != customer.CitizenDocumentNumber
            || document.DocumentType != customer.CitizenDocumentType
            || document.IssuingCountryCode != customer.CitizenIssuingCountryCode;
        if (documentChanged && await customers.CitizenDocumentExistsAsync(
                document.DocumentType,
                document.IssuingCountryCode,
                document.Number,
                customer.Id,
                cancellationToken
            ))
            throw new FridayException(
                CustomerErrorCodes.DocumentConflict,
                "Citizen document is already assigned to another Customer.",
                409
            );

        string previousFullName = customer.FullName;
        DateOnly? previousDateOfBirth = customer.DateOfBirth;
        string previousCitizenIdMasked = customer.CitizenIdMasked;
        try
        {
            customer.UpdateProfile(request.FullName, request.DateOfBirth, document);
        }
        catch (InvalidOperationException ex)
        {
            throw new FridayException(CustomerErrorCodes.Closed, ex.Message, 409);
        }

        DateTime occurredOnUtc = timeProvider.GetUtcNow().UtcDateTime;
        await audits.AddAsync(CustomerChangeAudit.Create(
            customer,
            "CUSTOMER_PROFILE_UPDATED",
            actor.UserId,
            CustomerAuditChangeSet.ProfileUpdated(
                previousFullName,
                previousDateOfBirth,
                previousCitizenIdMasked,
                customer
            ),
            null,
            null,
            null,
            "SUCCESS",
            actor.TraceId,
            occurredOnUtc,
            retention.GetRetainUntilUtc(occurredOnUtc)
        ), cancellationToken);

        return CustomerDetailDto.FromCustomer(customer);
    }

    private static CitizenDocument ResolveDocument(
        UpdateCustomerProfileCommand request,
        CustomerAggregate customer
    )
    {
        bool omitted = request.DocumentType is null
            && request.IssuingCountryCode is null
            && request.CitizenDocumentNumber is null;
        if (omitted)
            return CitizenDocument.Create(
                customer.CitizenDocumentType,
                customer.CitizenIssuingCountryCode,
                customer.CitizenDocumentNumber
            );

        if (request.DocumentType is null
            || string.IsNullOrWhiteSpace(request.IssuingCountryCode)
            || string.IsNullOrWhiteSpace(request.CitizenDocumentNumber))
            throw new ArgumentException("All Citizen document fields are required when replacing the document.");

        return CitizenDocument.Create(
            request.DocumentType.Value,
            request.IssuingCountryCode,
            request.CitizenDocumentNumber
        );
    }

    private static void EnsureExpectedVersion(CustomerAggregate customer, long expectedVersion)
    {
        if (customer.Version != expectedVersion)
            throw new FridayException(
                CustomerErrorCodes.ConcurrencyConflict,
                "Customer has changed; reload it and retry.",
                409
            );
    }
}
