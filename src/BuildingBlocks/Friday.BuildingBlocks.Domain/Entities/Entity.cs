using Friday.BuildingBlocks.Domain.Abstractions;

namespace Friday.BuildingBlocks.Domain.Entities;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public int Id { get; set; }
    public DateTime CreatedOnUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedOnUtc { get; private set; } = DateTime.UtcNow;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public void Touch() => UpdatedOnUtc = DateTime.UtcNow;
}
