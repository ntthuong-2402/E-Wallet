using FluentMigrator.Runner.Conventions;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.VersionTableInfo;
using Microsoft.Extensions.Options;

namespace Friday.BuildingBlocks.Infrastructure.DataMigrations;

/// <summary>
/// Version table metadata for FluentMigrator. The default layout already includes
/// <c>Version</c>, <c>AppliedOn</c>, and <c>Description</c>; this type pins it to <c>dbo.VersionInfo</c>
/// and remains the place to override names or add conventions if you extend the table later.
/// </summary>
[VersionTableMetaData]
public sealed class FridayVersionTableMetaData : DefaultVersionTableMetaData
{
    public FridayVersionTableMetaData(
        IConventionSet conventionSet,
        IOptions<RunnerOptions> runnerOptions
    )
        : base(conventionSet, runnerOptions)
    {
        SchemaName = "dbo";
    }
}
