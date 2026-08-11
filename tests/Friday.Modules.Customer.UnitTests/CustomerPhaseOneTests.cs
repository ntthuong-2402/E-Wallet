using Friday.BuildingBlocks.Application.Abstractions;
using Friday.BuildingBlocks.Application.Behaviors;
using Friday.BuildingBlocks.Domain.Entities;
using Friday.BuildingBlocks.Infrastructure;
using Friday.Modules.Customer.Application.Persistence;
using Friday.Modules.Customer.Domain;
using Friday.Modules.Customer.Infrastructure;
using Friday.Modules.Customer.Infrastructure.Persistence;
using LinKit.Core.Cqrs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Friday.Modules.Customer.UnitTests;

public sealed class CustomerPhaseOneTests
{
    [Fact]
    public void CustomerCommand_SelectsCustomerUnitOfWorkKey()
    {
        IUnitOfWorkCommand command = new CustomerProbeCommand();

        Assert.Equal(CustomerPersistence.UnitOfWorkKey, command.UnitOfWorkKey);
    }

    [Fact]
    public void CustomerAssemblies_DoNotReferenceAdminModules()
    {
        IEnumerable<string> references = new[]
            {
                typeof(CustomerDomainAssemblyMarker).Assembly,
                typeof(Friday.Modules.Customer.Application.CustomerApplicationAssemblyMarker).Assembly,
                typeof(CustomerDbContext).Assembly,
            }
            .SelectMany(x => x.GetReferencedAssemblies())
            .Select(x => x.Name ?? string.Empty);

        Assert.DoesNotContain(references, x => x.StartsWith("Friday.Modules.Admin", StringComparison.Ordinal));
    }

    [Fact]
    public void UnitOfWorkResolver_RoutesCustomerAndLegacyCommandsToDifferentBoundaries()
    {
        ConfigurationManager configuration = new();
        configuration["Database:InMemoryDatabaseName"] = $"CustomerPhaseOne-{Guid.NewGuid():N}";

        ServiceCollection services = new();
        services.AddBuildingBlocksInfrastructure(configuration);
        services.AddCustomerInfrastructure(configuration);
        services.AddScoped<IDomainEventDispatcher, NoOpDomainEventDispatcher>();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IUnitOfWorkResolver resolver =
            scope.ServiceProvider.GetRequiredService<IUnitOfWorkResolver>();
        IUnitOfWork customer = resolver.Resolve(new CustomerProbeCommand());
        IUnitOfWork legacy = resolver.Resolve(new LegacyProbeCommand());

        Assert.IsAssignableFrom<ICustomerUnitOfWork>(customer);
        Assert.IsNotAssignableFrom<ICustomerUnitOfWork>(legacy);
        Assert.NotSame(customer, legacy);
        Assert.NotSame(
            scope.ServiceProvider.GetRequiredService<CustomerDbContext>(),
            scope.ServiceProvider.GetRequiredService<
                Friday.BuildingBlocks.Infrastructure.Persistence.FridayDbContext
            >()
        );
    }

    [Fact]
    public async Task TransactionBehavior_UsesResolvedUnitOfWork()
    {
        RecordingUnitOfWork unitOfWork = new();
        TransactionBehavior<CustomerProbeCommand, bool> behavior =
            new(new FixedUnitOfWorkResolver(unitOfWork));

        bool result = await behavior.HandleAsync(
            new CustomerProbeCommand(),
            () => Task.FromResult(true),
            CancellationToken.None
        );

        Assert.True(result);
        Assert.Equal(1, unitOfWork.BeginCount);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(0, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task TransactionBehavior_RollsBackResolvedUnitOfWorkOnFailure()
    {
        RecordingUnitOfWork unitOfWork = new();
        TransactionBehavior<CustomerProbeCommand, bool> behavior =
            new(new FixedUnitOfWorkResolver(unitOfWork));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.HandleAsync(
                new CustomerProbeCommand(),
                () => throw new InvalidOperationException("test failure"),
                CancellationToken.None
            )
        );

        Assert.Equal(1, unitOfWork.BeginCount);
        Assert.Equal(0, unitOfWork.CommitCount);
        Assert.Equal(1, unitOfWork.RollbackCount);
    }

    private sealed record CustomerProbeCommand : ICustomerCommand<bool>;

    private sealed record LegacyProbeCommand : ICommand<bool>;

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(
            IReadOnlyCollection<Entity> entities,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }

    private sealed class FixedUnitOfWorkResolver(IUnitOfWork unitOfWork) : IUnitOfWorkResolver
    {
        public IUnitOfWork Resolve(object request) => unitOfWork;
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int BeginCount { get; private set; }
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            BeginCount++;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }
    }
}
