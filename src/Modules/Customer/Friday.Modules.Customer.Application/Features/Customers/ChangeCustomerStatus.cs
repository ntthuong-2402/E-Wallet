using Friday.BuildingBlocks.Application.Exceptions;
using Friday.Modules.Customer.Application.Auditing;
using Friday.Modules.Customer.Application.Errors;
using Friday.Modules.Customer.Application.Models;
using Friday.Modules.Customer.Application.Persistence;
using Friday.Modules.Customer.Domain.Auditing;
using Friday.Modules.Customer.Domain.Customers;
using Friday.Modules.Customer.Domain.Repositories;
using LinKit.Core.Cqrs;
using CustomerAggregate = Friday.Modules.Customer.Domain.Customers.Customer;

namespace Friday.Modules.Customer.Application.Features.Customers;

public sealed record ChangeCustomerStatusCommand(
    int CustomerId,
    long ExpectedVersion,
    CustomerStatus TargetStatus,
    string Reason
) : ICustomerCommand<CustomerDetailDto>;

public sealed class ChangeCustomerStatusHandler(
    ICustomerRepository customers,
    ICustomerAuditRepository audits,
    ICustomerAuditRetentionPolicy retention,
    ICustomerActor actor,
    TimeProvider timeProvider
) : ICommandHandler<ChangeCustomerStatusCommand, CustomerDetailDto>
{
    public async Task<CustomerDetailDto> HandleAsync(
        ChangeCustomerStatusCommand request,
        CancellationToken cancellationToken
    )
    {
        string reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length > 500)
            throw new ArgumentException("Status reason cannot exceed 500 characters.", nameof(request.Reason));

        CustomerAggregate customer = await customers.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new FridayException(CustomerErrorCodes.NotFound, "Customer was not found.", 404);
        if (customer.Version != request.ExpectedVersion)
            throw new FridayException(
                CustomerErrorCodes.ConcurrencyConflict,
                "Customer has changed; reload it and retry.",
                409
            );

        CustomerStatus previous = customer.Status;
        try
        {
            switch (request.TargetStatus)
            {
                case CustomerStatus.Active:
                    customer.Reactivate(reason);
                    break;
                case CustomerStatus.Suspended:
                    customer.Suspend(reason);
                    break;
                case CustomerStatus.Closed:
                    customer.Close(reason);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported Customer status.");
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new FridayException(CustomerErrorCodes.InvalidStatusTransition, ex.Message, 409);
        }

        DateTime occurredOnUtc = timeProvider.GetUtcNow().UtcDateTime;
        await audits.AddAsync(CustomerChangeAudit.Create(
            customer,
            "CUSTOMER_STATUS_CHANGED",
            actor.UserId,
            CustomerAuditChangeSet.StatusChanged(previous, customer.Status),
            previous.ToString(),
            customer.Status.ToString(),
            reason,
            "SUCCESS",
            actor.TraceId,
            occurredOnUtc,
            retention.GetRetainUntilUtc(occurredOnUtc)
        ), cancellationToken);

        return CustomerDetailDto.FromCustomer(customer);
    }
}
