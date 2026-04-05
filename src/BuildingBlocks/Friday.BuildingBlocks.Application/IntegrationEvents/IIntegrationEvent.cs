using LinKit.Core.Cqrs;

namespace Friday.BuildingBlocks.Application.IntegrationEvents;

public interface IIntegrationEvent : INotification
{
    DateTime OccurredOnUtc { get; }
}
