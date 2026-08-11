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
        RelationalDatabaseProvider provider,
        string inMemoryDatabaseName
    )
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            options.UseInMemoryDatabase(
                string.IsNullOrWhiteSpace(inMemoryDatabaseName)
                    ? "Friday.Shared"
                    : inMemoryDatabaseName
            );
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
