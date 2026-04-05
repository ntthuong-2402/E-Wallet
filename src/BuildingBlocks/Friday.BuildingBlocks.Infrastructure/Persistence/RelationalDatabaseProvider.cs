namespace Friday.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Relational provider for the shared <see cref="FridayDbContext"/> and linq2db.</summary>
public enum RelationalDatabaseProvider
{
    SqlServer,
    PostgreSql,
    MySql,
    Oracle,
}
