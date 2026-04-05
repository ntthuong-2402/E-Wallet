using Friday.BuildingBlocks.Domain.Abstractions;

namespace Friday.Modules.Admin.Domain.Events;

public sealed record UserLockedDomainEvent(int UserId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
