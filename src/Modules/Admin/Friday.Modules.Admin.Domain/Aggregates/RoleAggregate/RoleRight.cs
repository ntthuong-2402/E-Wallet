namespace Friday.Modules.Admin.Domain.Aggregates.RoleAggregate;

public sealed class RoleRight
{
    private RoleRight() { }

    public int RoleId { get; private set; }
    public int RightId { get; private set; }

    public static RoleRight Create(int roleId, int rightId)
    {
        return new RoleRight { RoleId = roleId, RightId = rightId };
    }
}
