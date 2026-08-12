using Friday.BuildingBlocks.Application.Abstractions;
using Friday.BuildingBlocks.Application.Exceptions;
using Friday.BuildingBlocks.Domain.Entities;
using Friday.Modules.Customer.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Friday.Modules.Customer.Application.Errors;
using Npgsql;
using Friday.Modules.Customer.Infrastructure.Observability;
using Friday.Modules.Customer.Domain.Auditing;

namespace Friday.Modules.Customer.Infrastructure.Persistence;

internal sealed class CustomerUnitOfWork(
    CustomerDbContext dbContext,
    IDomainEventDispatcher domainEventDispatcher
) : ICustomerUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            CustomerTelemetry.ConcurrencyConflicts.Add(1);
            throw new ConcurrencyConflictException(
                "The persisted Customer state changed during this operation.",
                ex
            );
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        )
        {
            string constraint = postgres.ConstraintName ?? string.Empty;
            if (constraint.Contains("CustomerCode", StringComparison.Ordinal))
                throw new FridayException(
                    CustomerErrorCodes.CodeConflict,
                    "Customer code already exists.",
                    409
                );
            if (constraint.Contains("CitizenDocument", StringComparison.Ordinal))
            {
                CustomerTelemetry.DocumentConflicts.Add(1);
                throw new FridayException(
                    CustomerErrorCodes.DocumentConflict,
                    "Citizen document is already assigned to another Customer.",
                    409
                );
            }
            if (constraint.Contains("RefId", StringComparison.Ordinal))
                throw new FridayException(
                    CustomerErrorCodes.RefIdConflict,
                    "RefId already exists.",
                    409
                );
            if (constraint.Contains("AccountId", StringComparison.Ordinal)
                || constraint.Contains("CustomerId", StringComparison.Ordinal))
                throw new FridayException(
                    CustomerErrorCodes.AccountAlreadyLinked,
                    "Customer or account already has an active linkage.",
                    409
                );
            throw;
        }
        catch (DbUpdateException)
        {
            if (dbContext.ChangeTracker.Entries<CustomerChangeAudit>()
                .Any(x => x.State == EntityState.Added))
                CustomerTelemetry.AuditWriteFailures.Add(1);
            throw;
        }
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null || dbContext.Database.CurrentTransaction is not null)
        {
            return;
        }

        if (dbContext.Database.IsRelational())
        {
            _transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await FlushAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "The persisted Customer state changed during this operation.",
                ex
            );
        }

        List<Entity> domainEntities = dbContext
            .ChangeTracker.Entries<Entity>()
            .Where(x => x.Entity.DomainEvents.Count > 0)
            .Select(x => x.Entity)
            .ToList();

        if (domainEntities.Count > 0)
        {
            await domainEventDispatcher.DispatchAsync(domainEntities, cancellationToken);
        }

        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
            return;
        }

        dbContext.ChangeTracker.Clear();
    }
}
