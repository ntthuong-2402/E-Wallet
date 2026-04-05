using Friday.Modules.Sample.Domain.Repositories;
using Friday.Modules.Sample.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Friday.Modules.Sample.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSampleInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ITodoItemRepository, InMemoryTodoItemRepository>();
        return services;
    }
}
