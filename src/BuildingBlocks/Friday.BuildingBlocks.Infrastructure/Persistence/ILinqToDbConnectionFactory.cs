using LinqToDB.Data;

namespace Friday.BuildingBlocks.Infrastructure.Persistence;

public interface ILinqToDbConnectionFactory : IDisposable, IAsyncDisposable
{
    DataConnection GetOrCreateConnection();
}
