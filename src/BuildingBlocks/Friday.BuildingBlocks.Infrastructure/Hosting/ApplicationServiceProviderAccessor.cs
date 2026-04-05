using Microsoft.Extensions.DependencyInjection;

namespace Friday.BuildingBlocks.Infrastructure.Hosting;

/// <summary>
/// Static holder for the host root <see cref="IServiceProvider"/>. Call <see cref="SetRoot"/> once after <c>WebApplication.Build()</c>.
/// Prefer creating a new scope before resolving scoped services (e.g. <see cref="Microsoft.EntityFrameworkCore.DbContext"/>).
/// </summary>
public static class ApplicationServiceProviderAccessor
{
    private static IServiceProvider? _root;
    private static IServiceScopeFactory? _scopeFactory;

    public static IServiceProvider Root =>
        _root
        ?? throw new InvalidOperationException(
            "ApplicationServiceProviderAccessor.SetRoot must run after the host is built."
        );

    public static void SetRoot(IServiceProvider rootServices)
    {
        _root = rootServices;
        _scopeFactory = rootServices.GetRequiredService<IServiceScopeFactory>();
    }

    public static IServiceScope CreateScope() =>
        (
            _scopeFactory
            ?? throw new InvalidOperationException(
                "ApplicationServiceProviderAccessor.SetRoot was not called."
            )
        ).CreateScope();

    public static async Task ExecuteInNewScopeAsync(
        Func<IServiceProvider, CancellationToken, Task> action,
        CancellationToken cancellationToken = default
    )
    {
        IServiceScopeFactory factory =
            _scopeFactory
            ?? throw new InvalidOperationException(
                "ApplicationServiceProviderAccessor.SetRoot was not called."
            );
        await using AsyncServiceScope scope = factory.CreateAsyncScope();
        await action(scope.ServiceProvider, cancellationToken);
    }

    public static async Task<T> ExecuteInNewScopeAsync<T>(
        Func<IServiceProvider, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default
    )
    {
        IServiceScopeFactory factory =
            _scopeFactory
            ?? throw new InvalidOperationException(
                "ApplicationServiceProviderAccessor.SetRoot was not called."
            );
        await using AsyncServiceScope scope = factory.CreateAsyncScope();
        return await action(scope.ServiceProvider, cancellationToken);
    }
}
