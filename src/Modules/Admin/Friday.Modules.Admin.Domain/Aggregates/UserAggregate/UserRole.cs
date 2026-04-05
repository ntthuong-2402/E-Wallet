namespace Friday.Modules.Admin.Domain.Aggregates.UserAggregate;

public sealed class UserRole
{
    private UserRole() { }

    public int UserId { get; private set; }
    public int RoleId { get; private set; }

    public static UserRole Create(int userId, int roleId)
    {
        return new UserRole { UserId = userId, RoleId = roleId };
    }
}
