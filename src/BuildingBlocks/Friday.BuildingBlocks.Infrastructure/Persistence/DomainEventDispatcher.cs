using Friday.BuildingBlocks.Application.Abstractions;
using Friday.BuildingBlocks.Domain.Abstractions;
using Friday.BuildingBlocks.Domain.Entities;
using LinKit.Core.Cqrs;

namespace Friday.BuildingBlocks.Infrastructure.Persistence;

public sealed class DomainEventDispatcher(IMediator mediator) : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IReadOnlyCollection<Entity> entities,
        CancellationToken cancellationToken = default
    )
    {
        foreach (Entity entity in entities)
        {
            if (entity.DomainEvents.Count == 0)
            {
                continue;
            }

            foreach (IDomainEvent domainEvent in entity.DomainEvents)
            {
                await mediator.PublishAsync(
                    domainEvent,
                    PublishStrategy.Sequential,
                    cancellationToken
                );
            }

            entity.ClearDomainEvents();
        }
    }
}
