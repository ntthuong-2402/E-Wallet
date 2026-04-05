using LinKit.Core.Cqrs;
using Friday.Modules.Sample.Application.Models;
using Friday.Modules.Sample.Domain.Aggregates.TodoItemAggregate;
using Friday.Modules.Sample.Domain.Events;
using Friday.Modules.Sample.Domain.Repositories;

namespace Friday.Modules.Sample.Application.Features;

public sealed record CreateTodoItemCommand(string Title) : LinKit.Core.Cqrs.ICommand<TodoItemDto>;

public sealed class CreateTodoItemHandler(ITodoItemRepository repository, IMediator mediator)
    : ICommandHandler<CreateTodoItemCommand, TodoItemDto>
{
    private readonly ITodoItemRepository _repository = repository;
    private readonly IMediator _mediator = mediator;

    public async Task<TodoItemDto> HandleAsync(
        CreateTodoItemCommand request,
        CancellationToken cancellationToken
    )
    {
        TodoItem created = await _repository.AddAsync(TodoItem.Create(request.Title), cancellationToken);
        await _mediator.PublishAsync(
            new TodoItemCreatedDomainEvent(created.Id, created.Title),
            PublishStrategy.Sequential,
            cancellationToken
        );
        return new TodoItemDto(created.Id, created.Title, created.IsDone);
    }
}
