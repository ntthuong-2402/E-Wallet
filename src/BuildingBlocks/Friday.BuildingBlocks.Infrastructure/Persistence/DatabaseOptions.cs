namespace Friday.BuildingBlocks.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// EF Core and FluentMigrator target database. EF migrations in this repo are generated for PostgreSQL (Npgsql).
    /// </summary>
    public RelationalDatabaseProvider Provider { get; set; } = RelationalDatabaseProvider.PostgreSql;

    /// <summary>
    /// When true, runs <see cref="Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync"/> on the shared
    /// <see cref="FridayDbContext"/>, then FluentMigrator data migrations (same connection). Skipped for non-relational providers (e.g. in-memory).
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}
