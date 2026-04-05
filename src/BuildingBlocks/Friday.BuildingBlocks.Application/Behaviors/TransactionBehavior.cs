using Friday.BuildingBlocks.Application.Abstractions;
using LinKit.Core.Cqrs;

namespace Friday.BuildingBlocks.Application.Behaviors;

[CqrsBehavior(typeof(ICommand), 0)]
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            TResponse response = await next();
            await _unitOfWork.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
