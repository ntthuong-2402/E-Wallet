using LinKit.Core.Cqrs;
using Friday.Modules.Sample.Application.Models;
using Friday.Modules.Sample.Domain.Repositories;

namespace Friday.Modules.Sample.Application.Features;

public sealed record GetTodoItemsQuery() : LinKit.Core.Cqrs.IQuery<IReadOnlyList<TodoItemDto>>;

public sealed class GetTodoItemsHandler(ITodoItemRepository repository)
    : IQueryHandler<GetTodoItemsQuery, IReadOnlyList<TodoItemDto>>
{
    private readonly ITodoItemRepository _repository = repository;

    public async Task<IReadOnlyList<TodoItemDto>> HandleAsync(
        GetTodoItemsQuery request,
        CancellationToken cancellationToken
    )
    {
        var items = await _repository.ListAsync(cancellationToken);
        return items.Select(x => new TodoItemDto(x.Id, x.Title, x.IsDone)).ToArray();
    }
}
