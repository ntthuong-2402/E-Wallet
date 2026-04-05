using Friday.BuildingBlocks.Domain.Abstractions;

namespace Friday.Modules.Admin.Domain.Events;

public sealed record UserCreatedDomainEvent(int UserId, string Username, string Email) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
