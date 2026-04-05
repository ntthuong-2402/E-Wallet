using Friday.BuildingBlocks.Domain.Abstractions;

namespace Friday.Modules.Admin.Domain.Events;

public sealed record UserRoleAssignedDomainEvent(int UserId, int RoleId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
