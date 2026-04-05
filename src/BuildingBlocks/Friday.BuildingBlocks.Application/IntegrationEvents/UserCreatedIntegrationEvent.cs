namespace Friday.BuildingBlocks.Application.IntegrationEvents;

public sealed record UserCreatedIntegrationEvent(int UserId, string Username, string Email)
    : IIntegrationEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
