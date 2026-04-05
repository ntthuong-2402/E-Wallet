using Friday.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Friday.API;

/// <summary>
/// Ensures <c>dotnet ef</c> loads all module assemblies (Admin, Sample) so the full model is migrated.
/// Set <c>FRIDAY_DESIGN_TIME_PG</c> to override the PostgreSQL connection string.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FridayDbContext>
{
    public FridayDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<FridayDbContext> options = new();
        string connectionString =
            Environment.GetEnvironmentVariable("FRIDAY_DESIGN_TIME_PG")
            ?? "Host=127.0.0.1;Port=5432;Database=friday;Username=postgres;Password=postgres";
        options.UseNpgsql(connectionString);
        return new FridayDbContext(options.Options);
    }
}
