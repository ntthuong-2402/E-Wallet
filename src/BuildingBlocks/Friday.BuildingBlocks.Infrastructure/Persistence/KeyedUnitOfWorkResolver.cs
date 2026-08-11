using Friday.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.BuildingBlocks.Infrastructure.Persistence;

internal sealed class KeyedUnitOfWorkResolver(IServiceProvider services) : IUnitOfWorkResolver
{
    public IUnitOfWork Resolve(object request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request is IUnitOfWorkCommand command
            ? services.GetRequiredKeyedService<IUnitOfWork>(command.UnitOfWorkKey)
            : services.GetRequiredService<IUnitOfWork>();
    }
}
