using LinqToDB;
using LinqToDB.Data;

namespace Friday.BuildingBlocks.Infrastructure.Persistence;

public sealed class LinqToDbConnectionFactory : ILinqToDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly RelationalDatabaseProvider _provider;
    private DataConnection? _connection;

    public LinqToDbConnectionFactory(string connectionString, RelationalDatabaseProvider provider)
    {
        _connectionString = connectionString;
        _provider = provider;
    }

    public DataConnection GetOrCreateConnection()
    {
        if (_connection is not null)
        {
            return _connection;
        }

        DataOptions options = _provider switch
        {
            RelationalDatabaseProvider.SqlServer => new DataOptions().UseSqlServer(_connectionString),
            RelationalDatabaseProvider.PostgreSql => new DataOptions().UsePostgreSQL(_connectionString),
            RelationalDatabaseProvider.MySql => new DataOptions().UseMySql(_connectionString),
            RelationalDatabaseProvider.Oracle => new DataOptions().UseOracle(_connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(_provider), _provider, null),
        };
        _connection = new DataConnection(options);
        return _connection;
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }

    public ValueTask DisposeAsync()
    {
        if (_connection is null)
        {
            return ValueTask.CompletedTask;
        }

        ValueTask disposeTask = _connection.DisposeAsync();
        _connection = null;
        return disposeTask;
    }
}
