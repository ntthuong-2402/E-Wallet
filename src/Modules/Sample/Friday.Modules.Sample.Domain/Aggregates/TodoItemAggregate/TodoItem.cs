using Friday.BuildingBlocks.Domain.Entities;
namespace Friday.Modules.Sample.Domain.Aggregates.TodoItemAggregate;

public sealed class TodoItem : AggregateRoot
{
    private TodoItem() { }

    public string Title { get; private set; } = string.Empty;
    public bool IsDone { get; private set; }

    public static TodoItem Create(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        return new TodoItem { Title = title.Trim(), IsDone = false };
    }

    public void MarkDone()
    {
        IsDone = true;
        Touch();
    }
}
