using LinKit.Core.Cqrs;

namespace Friday.BuildingBlocks.Domain.Abstractions;

public interface IDomainEvent : INotification
{
    DateTime OccurredOnUtc { get; }
}
