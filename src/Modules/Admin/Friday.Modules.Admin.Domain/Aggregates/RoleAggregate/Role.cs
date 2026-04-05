using Friday.BuildingBlocks.Domain.Entities;
using Friday.Modules.Admin.Domain.Events;

namespace Friday.Modules.Admin.Domain.Aggregates.RoleAggregate;

public sealed class Role : AggregateRoot
{
    private readonly List<RoleRight> _roleRights = [];

    private Role() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<RoleRight> RoleRights => _roleRights.AsReadOnly();

    public static Role Create(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Role code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Role name is required.", nameof(name));
        }

        return new Role
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            IsActive = true,
        };
    }

    public void SetRights(IEnumerable<int> rightIds)
    {
        int[] values = rightIds.Where(x => x > 0).Distinct().ToArray();
        _roleRights.Clear();
        _roleRights.AddRange(values.Select(x => RoleRight.Create(Id, x)));
        Touch();
        Raise(new RoleRightsChangedDomainEvent(Id, values));
    }
}
