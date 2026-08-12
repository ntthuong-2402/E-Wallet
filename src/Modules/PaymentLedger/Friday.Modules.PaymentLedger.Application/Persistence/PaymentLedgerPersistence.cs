using Friday.BuildingBlocks.Application.Abstractions;
using LinKit.Core.Cqrs;

namespace Friday.Modules.PaymentLedger.Application.Persistence;

public static class PaymentLedgerPersistence
{
    public const string UnitOfWorkKey = "payment-ledger";
}

public interface IPaymentLedgerCommand<TResponse> : ICommand<TResponse>, IUnitOfWorkCommand
{
    string IUnitOfWorkCommand.UnitOfWorkKey => PaymentLedgerPersistence.UnitOfWorkKey;
}

public interface IPaymentLedgerUnitOfWork : IUnitOfWork { }
