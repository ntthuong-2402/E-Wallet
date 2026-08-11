namespace Friday.BuildingBlocks.Application.Authorization;

public interface IPermissionContribution
{
    IReadOnlyCollection<string> Permissions { get; }
}
