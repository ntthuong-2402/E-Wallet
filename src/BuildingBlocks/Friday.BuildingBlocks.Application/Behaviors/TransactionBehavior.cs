using Friday.BuildingBlocks.Application.Abstractions;
using LinKit.Core.Cqrs;

namespace Friday.BuildingBlocks.Application.Behaviors;

[CqrsBehavior(typeof(ICommand), 0)]
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWorkResolver unitOfWorkResolver)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        IUnitOfWork unitOfWork = unitOfWorkResolver.Resolve(request);
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            TResponse response = await next();
            await unitOfWork.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
