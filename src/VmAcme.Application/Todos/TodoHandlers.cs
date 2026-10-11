using MediatR;
using VmAcme.Authentication;
using VmAcme.Common;
using Unit = VmAcme.Common.Unit;

namespace VmAcme.Todos;

internal static class TodoErrors
{
    public static readonly Error Unauthorized =
        new("Authentication.Required", "Authentication is required.", ErrorType.Unauthorized);

    public static readonly Error NotFound = new("Todos.NotFound", "Todo was not found.", ErrorType.NotFound);
}

public sealed class CreateTodoHandler(ITodoRepository repository, IUserContext user, TimeProvider time)
    : IRequestHandler<CreateTodoCommand, Result<TodoResponse>>
{
    public async Task<Result<TodoResponse>> Handle(CreateTodoCommand request, CancellationToken cancellationToken)
    {
        if (user.UserId is not { } userId) return Result<TodoResponse>.Failure(TodoErrors.Unauthorized);
        var todo = TodoItem.Create(userId, request.Description!, request.Priority, request.DueDate,
            request.Labels ?? [], time.GetUtcNow());
        await repository.AddAsync(todo, cancellationToken);
        return Result<TodoResponse>.Success(TodoResponse.From(todo));
    }
}

public sealed class ListTodosHandler(ITodoRepository repository, IUserContext user)
    : IRequestHandler<ListTodosQuery, Result<IReadOnlyList<TodoResponse>>>
{
    public async Task<Result<IReadOnlyList<TodoResponse>>> Handle(ListTodosQuery request,
        CancellationToken cancellationToken)
    {
        if (user.UserId is not { } userId) return Result<IReadOnlyList<TodoResponse>>.Failure(TodoErrors.Unauthorized);
        var todos = await repository.ListAsync(userId, cancellationToken);
        return Result<IReadOnlyList<TodoResponse>>.Success(todos.Select(TodoResponse.From).ToArray());
    }
}

public sealed class GetTodoHandler(ITodoRepository repository, IUserContext user)
    : IRequestHandler<GetTodoQuery, Result<TodoResponse>>
{
    public async Task<Result<TodoResponse>> Handle(GetTodoQuery request, CancellationToken cancellationToken)
    {
        if (user.UserId is not { } userId) return Result<TodoResponse>.Failure(TodoErrors.Unauthorized);
        var todo = await repository.GetAsync(request.Id, userId, cancellationToken);
        return todo is null
            ? Result<TodoResponse>.Failure(TodoErrors.NotFound)
            : Result<TodoResponse>.Success(TodoResponse.From(todo));
    }
}

public sealed class CompleteTodoHandler(ITodoRepository repository, IUserContext user, TimeProvider time)
    : IRequestHandler<CompleteTodoCommand, Result<TodoResponse>>
{
    public async Task<Result<TodoResponse>> Handle(CompleteTodoCommand request, CancellationToken cancellationToken)
    {
        if (user.UserId is not { } userId) return Result<TodoResponse>.Failure(TodoErrors.Unauthorized);
        var todo = await repository.GetAsync(request.Id, userId, cancellationToken);
        if (todo is null) return Result<TodoResponse>.Failure(TodoErrors.NotFound);
        var completed = todo.Complete(time.GetUtcNow());
        if (completed.IsFailure) return Result<TodoResponse>.Failure(completed.Error!);
        var saved = await repository.UpdateAsync(todo, cancellationToken);
        return saved.IsFailure
            ? Result<TodoResponse>.Failure(saved.Error!)
            : Result<TodoResponse>.Success(TodoResponse.From(todo));
    }
}

public sealed class DeleteTodoHandler(ITodoRepository repository, IUserContext user)
    : IRequestHandler<DeleteTodoCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(DeleteTodoCommand request, CancellationToken cancellationToken)
    {
        if (user.UserId is not { } userId) return Result<Unit>.Failure(TodoErrors.Unauthorized);
        var todo = await repository.GetAsync(request.Id, userId, cancellationToken);
        return todo is null
            ? Result<Unit>.Failure(TodoErrors.NotFound)
            : await repository.DeleteAsync(todo, cancellationToken);
    }
}