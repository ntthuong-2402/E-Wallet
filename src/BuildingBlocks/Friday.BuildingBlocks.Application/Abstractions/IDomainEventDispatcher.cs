using Friday.BuildingBlocks.Domain.Entities;

namespace Friday.BuildingBlocks.Application.Abstractions;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IReadOnlyCollection<Entity> entities,
        CancellationToken cancellationToken = default
    );
}
