namespace Friday.BuildingBlocks.Application.Abstractions;

public interface IUnitOfWorkResolver
{
    IUnitOfWork Resolve(object request);
}
