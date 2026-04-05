using Friday.BuildingBlocks.Domain.Abstractions;

namespace Friday.Modules.Admin.Domain.Events;

public sealed record RoleRightsChangedDomainEvent(int RoleId, int[] RightIds) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
