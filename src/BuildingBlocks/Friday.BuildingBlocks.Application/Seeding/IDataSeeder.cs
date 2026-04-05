namespace Friday.BuildingBlocks.Application.Seeding;

/// <summary>
/// Application startup (or dedicated job) seeding. Implementations should be idempotent (safe to run repeatedly).
/// </summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
