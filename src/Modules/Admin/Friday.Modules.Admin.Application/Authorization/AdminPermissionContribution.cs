using Friday.BuildingBlocks.Application.Authorization;

namespace Friday.Modules.Admin.Application.Authorization;

public sealed class AdminPermissionContribution : IPermissionContribution
{
    public IReadOnlyCollection<string> Permissions => AdminPermissions.All;
}
