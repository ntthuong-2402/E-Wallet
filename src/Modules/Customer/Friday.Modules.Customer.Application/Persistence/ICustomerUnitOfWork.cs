using Friday.BuildingBlocks.Application.Abstractions;

namespace Friday.Modules.Customer.Application.Persistence;

public interface ICustomerUnitOfWork : IUnitOfWork
{
    Task FlushAsync(CancellationToken cancellationToken = default);
}
