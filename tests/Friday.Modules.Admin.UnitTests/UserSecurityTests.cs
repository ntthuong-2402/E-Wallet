using Friday.Modules.Admin.Domain.Aggregates.UserAggregate;
using Xunit;

namespace Friday.Modules.Admin.UnitTests;

public sealed class UserSecurityTests
{
    [Fact]
    public void New_admin_provisioned_user_must_change_password()
    {
        User user = CreateUser();

        Assert.True(user.MustChangePassword);
        Assert.True(user.IsActive);
        Assert.False(user.IsLocked);
    }

    [Fact]
    public void Failed_attempt_threshold_temporarily_locks_user()
    {
        User user = CreateUser();
        DateTime now = new(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            user.RecordLoginFailure(now, 5, TimeSpan.FromMinutes(15));
        }

        Assert.True(user.IsTemporarilyLocked(now.AddMinutes(14)));
        Assert.False(user.IsTemporarilyLocked(now.AddMinutes(16)));
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void Assigned_role_can_be_removed()
    {
        User user = CreateUser();
        user.AssignRole(42);

        user.RemoveRole(42);

        Assert.DoesNotContain(user.UserRoles, x => x.RoleId == 42);
    }

    [Fact]
    public void Refresh_rotation_preserves_family_and_revokes_old_session()
    {
        UserSession original = UserSession.Create(
            1,
            new string('a', 64),
            DateTime.UtcNow.AddDays(1),
            null,
            null
        );

        UserSession replacement = original.ReplaceWith(
            new string('b', 64),
            DateTime.UtcNow.AddDays(2)
        );

        Assert.Equal(original.TokenFamilyId, replacement.TokenFamilyId);
        Assert.NotNull(original.RevokedAtUtc);
        Assert.NotNull(original.ReplacedAtUtc);
        Assert.Equal(1, original.Version);
    }

    private static User CreateUser() =>
        User.Create("USR001", "admin.user", "admin@example.com", "Admin User", null, null, null, null, null);
}
