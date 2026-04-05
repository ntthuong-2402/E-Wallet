using Friday.BuildingBlocks.Domain.Entities;

namespace Friday.Modules.Admin.Domain.Aggregates.RightAggregate;

public sealed class Right : AggregateRoot
{
    private Right() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    public static Right Create(string code, string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Right code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Right name is required.", nameof(name));
        }

        return new Right
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
        };
    }
}
