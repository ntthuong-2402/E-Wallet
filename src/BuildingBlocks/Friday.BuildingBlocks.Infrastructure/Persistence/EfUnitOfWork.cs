using Friday.BuildingBlocks.Application.Abstractions;
using Friday.BuildingBlocks.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Friday.BuildingBlocks.Infrastructure.Persistence;

public sealed class EfUnitOfWork(
    FridayDbContext dbContext,
    IDomainEventDispatcher domainEventDispatcher
) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null || dbContext.Database.CurrentTransaction is not null)
        {
            return;
        }

        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        _transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);

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
