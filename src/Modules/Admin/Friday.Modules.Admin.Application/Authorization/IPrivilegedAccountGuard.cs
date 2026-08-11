namespace Friday.Modules.Admin.Application.Authorization;

public interface IPrivilegedAccountGuard
{
    Task EnsureCanDisableAsync(int userId, CancellationToken cancellationToken = default);
    Task EnsureCanRemoveRoleAsync(
        int userId,
        int roleId,
        CancellationToken cancellationToken = default
    );
}
