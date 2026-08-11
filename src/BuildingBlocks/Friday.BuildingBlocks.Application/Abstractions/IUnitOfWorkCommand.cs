namespace Friday.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Selects a named unit of work for a command that does not belong to the
/// legacy shared persistence boundary.
/// </summary>
public interface IUnitOfWorkCommand
{
    string UnitOfWorkKey { get; }
}
