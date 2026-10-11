using VmAcme.Common;

namespace VmAcme.Todos;

public sealed class TodoItem : AggregateRoot<Guid>
{
    private readonly object _completionLock = new();

    private TodoItem()
    {
    }

    public Guid UserId { get; private set; }
    public string Description { get; private set; } = "";
    public Priority Priority { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public string[] Labels { get; private set; } = [];
    public bool IsCompleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static TodoItem Create(Guid userId, string description, Priority priority,
        DateTimeOffset? dueDate, string[] labels, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Description = description.Trim(), Priority = priority,
        DueDate = dueDate?.ToUniversalTime(), Labels = [.. labels], CreatedAt = now.ToUniversalTime()
    };

    public Result<Unit> Complete(DateTimeOffset now)
    {
        lock (_completionLock)
        {
            if (IsCompleted)
                return Result<Unit>.Failure(new Error("Todos.AlreadyCompleted", "Todo is already completed.",
                    ErrorType.Conflict));
            IsCompleted = true;
            CompletedAt = now.ToUniversalTime();
            return Result<Unit>.Success(new Unit());
        }
    }
}