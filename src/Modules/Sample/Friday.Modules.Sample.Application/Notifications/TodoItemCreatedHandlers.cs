using Friday.Modules.Sample.Domain.Events;
using LinKit.Core.Cqrs;
using Microsoft.Extensions.Logging;

namespace Friday.Modules.Sample.Application.Notifications;

public sealed class TodoItemCreatedAuditHandler(ILogger<TodoItemCreatedAuditHandler> logger)
    : INotificationHandler<TodoItemCreatedDomainEvent>
{
    private readonly ILogger<TodoItemCreatedAuditHandler> _logger = logger;

    public Task HandleAsync(
        TodoItemCreatedDomainEvent notification,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "Audit event: todo item created with id {TodoItemId}, title {Title}",
            notification.TodoItemId,
            notification.Title
        );
        return Task.CompletedTask;
    }
}

public sealed class TodoItemCreatedProjectionHandler(ILogger<TodoItemCreatedProjectionHandler> logger)
    : INotificationHandler<TodoItemCreatedDomainEvent>
{
    private readonly ILogger<TodoItemCreatedProjectionHandler> _logger = logger;

    public Task HandleAsync(
        TodoItemCreatedDomainEvent notification,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "Projection event: refresh read model for todo item {TodoItemId}",
            notification.TodoItemId
        );
        return Task.CompletedTask;
    }
}
