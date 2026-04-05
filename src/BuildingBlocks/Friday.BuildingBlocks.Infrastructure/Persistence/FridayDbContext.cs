using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Friday.BuildingBlocks.Infrastructure.Persistence;

public sealed class FridayDbContext(DbContextOptions<FridayDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Explicit list so <c>dotnet ef</c> and minimal hosts load module EF configurations (not only whatever is already in <see cref="AppDomain"/>).
    /// </summary>
    private static readonly string[] ModuleConfigurationAssemblyNames =
    [
        "Friday.Modules.Admin.Infrastructure",
        "Friday.Modules.Sample.Infrastructure",
    ];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FridayDbContext).Assembly);

        foreach (string name in ModuleConfigurationAssemblyNames)
        {
            try
            {
                Assembly assembly = Assembly.Load(name);
                modelBuilder.ApplyConfigurationsFromAssembly(assembly);
            }
            catch (FileNotFoundException)
            {
                // Slim deployment without that module.
            }
        }
    }
}
