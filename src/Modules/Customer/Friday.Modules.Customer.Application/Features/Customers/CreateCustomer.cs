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

public sealed record CreateCustomerCommand(
    string FullName,
    DateOnly? DateOfBirth,
    CitizenDocumentType DocumentType,
    string IssuingCountryCode,
    string CitizenDocumentNumber
) : ICustomerCommand<CustomerDetailDto>;

public sealed class CreateCustomerHandler(
    ICustomerRepository customers,
    ICustomerAuditRepository audits,
    ICustomerCodeGenerator codeGenerator,
    ICustomerAuditRetentionPolicy retention,
    ICustomerActor actor,
    ICustomerUnitOfWork unitOfWork,
    TimeProvider timeProvider
) : ICommandHandler<CreateCustomerCommand, CustomerDetailDto>
{
    private const int CodeGenerationAttempts = 5;

    public async Task<CustomerDetailDto> HandleAsync(
        CreateCustomerCommand request,
        CancellationToken cancellationToken
    )
    {
        CitizenDocument document = CitizenDocument.Create(
            request.DocumentType,
            request.IssuingCountryCode,
            request.CitizenDocumentNumber
        );
        if (await customers.CitizenDocumentExistsAsync(
            document.DocumentType,
            document.IssuingCountryCode,
            document.Number,
            cancellationToken: cancellationToken
        ))
        {
            throw new FridayException(
                CustomerErrorCodes.DocumentConflict,
                "Citizen document is already assigned to another Customer.",
                409
            );
        }

        string customerCode = await GenerateAvailableCodeAsync(cancellationToken);
        DateTime occurredOnUtc = timeProvider.GetUtcNow().UtcDateTime;
        CustomerAggregate customer = CustomerAggregate.Create(
            customerCode,
            request.FullName,
            request.DateOfBirth,
            document,
            occurredOnUtc
        );
        await customers.AddAsync(customer, cancellationToken);

        // Flush obtains the database identity while the transaction remains open.
        // The final transaction commit still includes both Customer and audit.
        await unitOfWork.FlushAsync(cancellationToken);
        CustomerChangeAudit audit = CustomerChangeAudit.Create(
            customer,
            "CUSTOMER_CREATED",
            actor.UserId,
            CustomerAuditChangeSet.Created(customer),
            null,
            CustomerStatus.Active.ToString(),
            null,
            "SUCCESS",
            actor.TraceId,
            occurredOnUtc,
            retention.GetRetainUntilUtc(occurredOnUtc)
        );
        await audits.AddAsync(audit, cancellationToken);

        return CustomerDetailDto.FromCustomer(customer);
    }

    private async Task<string> GenerateAvailableCodeAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < CodeGenerationAttempts; attempt++)
        {
            string code = codeGenerator.Generate();
            if (!await customers.CustomerCodeExistsAsync(code, cancellationToken))
                return code;
        }

        throw new FridayException(
            CustomerErrorCodes.CodeConflict,
            "Could not allocate a unique Customer code.",
            409
        );
    }
}
