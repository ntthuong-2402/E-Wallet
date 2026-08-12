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

public sealed record LinkCustomerAccountCommand(
    int CustomerId,
    long ExpectedVersion,
    string AccountId,
    string Reason
) : ICustomerCommand<CustomerAccountLinkageDto>;

public sealed class LinkCustomerAccountHandler(
    ICustomerRepository customers,
    ICustomerAccountLinkageRepository linkages,
    IExternalAccountDirectory accountDirectory,
    ICustomerAuditRepository audits,
    ICustomerAuditRetentionPolicy retention,
    ICustomerActor actor,
    TimeProvider timeProvider,
    ICustomerUnitOfWork unitOfWork
) : ICommandHandler<LinkCustomerAccountCommand, CustomerAccountLinkageDto>
{
    public async Task<CustomerAccountLinkageDto> HandleAsync(
        LinkCustomerAccountCommand request,
        CancellationToken cancellationToken
    )
    {
        CustomerAggregate customer = await customers.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new FridayException(CustomerErrorCodes.NotFound, "Customer was not found.", 404);
        EnsureVersion(customer, request.ExpectedVersion);

        string accountId = CustomerAccountLinkage.NormalizeAccountId(request.AccountId);
        if (!await accountDirectory.ExistsAsync(accountId, cancellationToken))
            throw new FridayException(
                CustomerErrorCodes.AccountNotFound,
                "Account was not found.",
                404
            );
        if (await linkages.GetActiveByCustomerIdAsync(customer.Id, cancellationToken) is not null
            || await linkages.GetActiveByAccountIdAsync(accountId, cancellationToken) is not null)
            throw new FridayException(
                CustomerErrorCodes.AccountAlreadyLinked,
                "Customer or account already has an active linkage.",
                409
            );

        DateTime occurredOnUtc = timeProvider.GetUtcNow().UtcDateTime;
        CustomerAccountLinkage linkage = CustomerAccountLinkage.Link(
            customer,
            accountId,
            actor.UserId,
            request.Reason,
            occurredOnUtc
        );
        try
        {
            customer.RegisterAccountLinkageChange();
        }
        catch (InvalidOperationException ex)
        {
            throw new FridayException(CustomerErrorCodes.Closed, ex.Message, 409);
        }
        await linkages.AddAsync(linkage, cancellationToken);
        await audits.AddAsync(CustomerChangeAudit.Create(
            customer,
            "CUSTOMER_ACCOUNT_LINKED",
            actor.UserId,
            CustomerAuditChangeSet.AccountLinked(accountId),
            null,
            null,
            request.Reason,
            "SUCCESS",
            actor.TraceId,
            occurredOnUtc,
            retention.GetRetainUntilUtc(occurredOnUtc)
        ), cancellationToken);

        await unitOfWork.FlushAsync(cancellationToken);
        return CustomerAccountLinkageDto.FromLinkage(linkage, customer.Version);
    }

    private static void EnsureVersion(CustomerAggregate customer, long expectedVersion)
    {
        if (customer.Version != expectedVersion)
            throw new FridayException(
                CustomerErrorCodes.ConcurrencyConflict,
                "Customer has changed; reload it and retry.",
                409
            );
    }
}

public sealed record UnlinkCustomerAccountCommand(
    int CustomerId,
    long ExpectedVersion,
    string Reason
) : ICustomerCommand<bool>;

public sealed class UnlinkCustomerAccountHandler(
    ICustomerRepository customers,
    ICustomerAccountLinkageRepository linkages,
    ICustomerAuditRepository audits,
    ICustomerAuditRetentionPolicy retention,
    ICustomerActor actor,
    TimeProvider timeProvider
) : ICommandHandler<UnlinkCustomerAccountCommand, bool>
{
    public async Task<bool> HandleAsync(
        UnlinkCustomerAccountCommand request,
        CancellationToken cancellationToken
    )
    {
        CustomerAggregate customer = await customers.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new FridayException(CustomerErrorCodes.NotFound, "Customer was not found.", 404);
        if (customer.Version != request.ExpectedVersion)
            throw new FridayException(
                CustomerErrorCodes.ConcurrencyConflict,
                "Customer has changed; reload it and retry.",
                409
            );
        CustomerAccountLinkage linkage =
            await linkages.GetActiveByCustomerIdAsync(customer.Id, cancellationToken)
            ?? throw new FridayException(
                CustomerErrorCodes.AccountLinkageNotFound,
                "Customer account linkage was not found.",
                404
            );

        DateTime occurredOnUtc = timeProvider.GetUtcNow().UtcDateTime;
        linkage.Unlink(actor.UserId, request.Reason, occurredOnUtc);
        customer.RegisterAccountLinkageChange(allowWhenClosed: true);
        await audits.AddAsync(CustomerChangeAudit.Create(
            customer,
            "CUSTOMER_ACCOUNT_UNLINKED",
            actor.UserId,
            CustomerAuditChangeSet.AccountUnlinked(linkage.AccountId),
            null,
            null,
            request.Reason,
            "SUCCESS",
            actor.TraceId,
            occurredOnUtc,
            retention.GetRetainUntilUtc(occurredOnUtc)
        ), cancellationToken);
        return true;
    }
}

public sealed record GetCustomerAccountLinkageQuery(int CustomerId)
    : IQuery<CustomerAccountLinkageDto>;

public sealed class GetCustomerAccountLinkageHandler(
    ICustomerRepository customers,
    ICustomerAccountLinkageRepository linkages
) : IQueryHandler<GetCustomerAccountLinkageQuery, CustomerAccountLinkageDto>
{
    public async Task<CustomerAccountLinkageDto> HandleAsync(
        GetCustomerAccountLinkageQuery request,
        CancellationToken cancellationToken
    )
    {
        CustomerAggregate customer = await customers.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new FridayException(CustomerErrorCodes.NotFound, "Customer was not found.", 404);
        CustomerAccountLinkage linkage =
            await linkages.GetActiveByCustomerIdAsync(request.CustomerId, cancellationToken)
            ?? throw new FridayException(
                CustomerErrorCodes.AccountLinkageNotFound,
                "Customer account linkage was not found.",
                404
            );
        return CustomerAccountLinkageDto.FromLinkage(linkage, customer.Version);
    }
}

public sealed record GetMyCustomerQuery : IQuery<CustomerDetailDto>;

public sealed class GetMyCustomerHandler(ICustomerReadStore store, ICustomerActor actor)
    : IQueryHandler<GetMyCustomerQuery, CustomerDetailDto>
{
    public async Task<CustomerDetailDto> HandleAsync(
        GetMyCustomerQuery request,
        CancellationToken cancellationToken
    ) => await store.GetByAccountIdAsync(actor.UserId, cancellationToken)
        ?? throw new FridayException(
            CustomerErrorCodes.AccountLinkageNotFound,
            "Authenticated account is not linked to a Customer.",
            404
        );
}
