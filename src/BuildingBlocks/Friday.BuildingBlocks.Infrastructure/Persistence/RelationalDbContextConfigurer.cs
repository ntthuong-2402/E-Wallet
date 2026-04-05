using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.Extensions;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Oracle.EntityFrameworkCore;

namespace Friday.BuildingBlocks.Infrastructure.Persistence;

internal static class RelationalDbContextConfigurer
{
    public static void Configure(
        DbContextOptionsBuilder options,
        string? connectionString,
        RelationalDatabaseProvider provider
    )
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            options.UseInMemoryDatabase("Friday.Shared");
            return;
        }

        switch (provider)
        {
            case RelationalDatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString);
                break;
            case RelationalDatabaseProvider.PostgreSql:
                options.UseNpgsql(connectionString);
                break;
            case RelationalDatabaseProvider.MySql:
                options.UseMySQL(connectionString);
                break;
            case RelationalDatabaseProvider.Oracle:
                options.UseOracle(connectionString);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown database provider.");
        }
    }
}
