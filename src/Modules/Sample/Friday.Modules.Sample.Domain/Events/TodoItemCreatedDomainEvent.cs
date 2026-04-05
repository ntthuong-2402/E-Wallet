using Friday.BuildingBlocks.Domain.Abstractions;

namespace Friday.Modules.Sample.Domain.Events;

public sealed record TodoItemCreatedDomainEvent(int TodoItemId, string Title) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
