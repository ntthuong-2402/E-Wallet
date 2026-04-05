using Friday.BuildingBlocks.Application.IntegrationEvents;
using LinKit.Core.Cqrs;
using Microsoft.Extensions.Logging;

namespace Friday.Modules.Sample.Application.Notifications;

public sealed class UserCreatedIntegrationEventHandler(
    ILogger<UserCreatedIntegrationEventHandler> logger
) : INotificationHandler<UserCreatedIntegrationEvent>
{
    public Task HandleAsync(
        UserCreatedIntegrationEvent notification,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Sample integration event: user created in Admin. UserId={UserId}, Username={Username}",
            notification.UserId,
            notification.Username
        );
        return Task.CompletedTask;
    }
}
