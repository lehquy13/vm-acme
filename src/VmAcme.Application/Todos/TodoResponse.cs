namespace VmAcme.Todos;

public sealed record TodoResponse(
    Guid Id,
    Guid UserId,
    string Description,
    Priority Priority,
    DateTimeOffset? DueDate,
    string[] Labels,
    bool IsCompleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static TodoResponse From(TodoItem todo) => new(todo.Id, todo.UserId, todo.Description, todo.Priority,
        todo.DueDate, [.. todo.Labels], todo.IsCompleted, todo.CreatedAt, todo.CompletedAt);
}