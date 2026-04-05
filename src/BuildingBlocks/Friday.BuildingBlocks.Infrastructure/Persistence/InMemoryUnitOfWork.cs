using Friday.BuildingBlocks.Application.Abstractions;

namespace Friday.BuildingBlocks.Infrastructure.Persistence;

public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
