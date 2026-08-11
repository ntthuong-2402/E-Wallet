using Friday.BuildingBlocks.Application.Abstractions;
using LinKit.Core.Cqrs;

namespace Friday.Modules.Customer.Application.Persistence;

public interface ICustomerCommand<TResponse> : ICommand<TResponse>, IUnitOfWorkCommand
{
    string IUnitOfWorkCommand.UnitOfWorkKey => CustomerPersistence.UnitOfWorkKey;
}
