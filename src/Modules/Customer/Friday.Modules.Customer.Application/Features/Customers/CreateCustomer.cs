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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Friday.Modules.Customer.Application.Features.Customers;

public sealed record CreateCustomerCommand(
    string RefId,
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
    TimeProvider timeProvider,
    ICustomerCreateReferenceRepository references
) : ICommandHandler<CreateCustomerCommand, CustomerDetailDto>
{
    private const int CodeGenerationAttempts = 5;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CustomerDetailDto> HandleAsync(
        CreateCustomerCommand request,
        CancellationToken cancellationToken
    )
    {
        string refId = CustomerCreateReference.NormalizeRefId(request.RefId);
        CitizenDocument document = CitizenDocument.Create(
            request.DocumentType,
            request.IssuingCountryCode,
            request.CitizenDocumentNumber
        );
        string requestHash = ComputeRequestHash(request, document);
        await references.AcquireAsync(refId, cancellationToken);
        CustomerCreateReference? existingReference = await references.GetAsync(refId, cancellationToken);
        if (existingReference is not null)
        {
            if (!string.Equals(existingReference.ActorUserId, actor.UserId, StringComparison.Ordinal)
                || !string.Equals(existingReference.RequestHash, requestHash, StringComparison.Ordinal))
                throw new FridayException(
                    CustomerErrorCodes.RefIdConflict,
                    "RefId was already used with a different Customer request.",
                    409
                );
            return JsonSerializer.Deserialize<CustomerDetailDto>(
                existingReference.ResponseJson,
                JsonOptions
            ) ?? throw new InvalidOperationException("Stored Customer create response is invalid.");
        }

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

        CustomerDetailDto response = CustomerDetailDto.FromCustomer(customer);
        await references.AddAsync(CustomerCreateReference.Create(
            refId,
            requestHash,
            actor.UserId,
            customer,
            JsonSerializer.Serialize(response, JsonOptions),
            occurredOnUtc
        ), cancellationToken);
        return response;
    }

    private static string ComputeRequestHash(CreateCustomerCommand request, CitizenDocument document)
    {
        string normalizedName = string.Join(' ', (request.FullName ?? string.Empty).Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        ));
        string canonical = string.Join('\n',
            normalizedName,
            request.DateOfBirth?.ToString("yyyy-MM-dd") ?? string.Empty,
            ((int)document.DocumentType).ToString(System.Globalization.CultureInfo.InvariantCulture),
            document.IssuingCountryCode,
            document.Number
        );
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
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
