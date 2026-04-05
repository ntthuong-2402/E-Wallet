using Friday.Modules.Sample.Domain.Aggregates.TodoItemAggregate;
using Friday.Modules.Sample.Domain.Repositories;

namespace Friday.Modules.Sample.Infrastructure.Repositories;

public sealed class InMemoryTodoItemRepository : ITodoItemRepository
{
    private static int _seed;
    private static readonly List<TodoItem> Storage = [];

    public Task<TodoItem> AddAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        int id = Interlocked.Increment(ref _seed);
        item.Id = id;
        Storage.Add(item);
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<TodoItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TodoItem> snapshot = Storage.OrderByDescending(x => x.Id).ToArray();
        return Task.FromResult(snapshot);
    }
}
