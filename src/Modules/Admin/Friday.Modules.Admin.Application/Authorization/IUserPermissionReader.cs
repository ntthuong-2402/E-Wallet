namespace Friday.Modules.Admin.Application.Authorization;

public interface IUserPermissionReader
{
    Task<bool> HasPermissionAsync(
        int userId,
        string permission,
        CancellationToken cancellationToken = default
    );
}
