using Friday.Modules.Sample.Domain.Aggregates.TodoItemAggregate;

namespace Friday.Modules.Sample.Domain.Repositories;

public interface ITodoItemRepository
{
    Task<TodoItem> AddAsync(TodoItem item, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TodoItem>> ListAsync(CancellationToken cancellationToken = default);
}
