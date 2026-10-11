using MediatR;
using VmAcme.Common;
using Unit = VmAcme.Common.Unit;

namespace VmAcme.Todos;

public sealed record CreateTodoCommand(
    string? Description,
    Priority Priority,
    DateTimeOffset? DueDate,
    string[]? Labels)
    : IRequest<Result<TodoResponse>>;

public sealed record ListTodosQuery : IRequest<Result<IReadOnlyList<TodoResponse>>>;

public sealed record GetTodoQuery(Guid Id) : IRequest<Result<TodoResponse>>;

public sealed record CompleteTodoCommand(Guid Id) : IRequest<Result<TodoResponse>>;

public sealed record DeleteTodoCommand(Guid Id) : IRequest<Result<Unit>>;