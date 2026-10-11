using FastEndpoints;
using MediatR;
using VmAcme.Infrastructure;
using VmAcme.Todos;

namespace VmAcme.Endpoints.Todos;

public sealed class CreateTodoRequest
{
    public string? Description { get; set; }
    public Priority Priority { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public string[]? Labels { get; set; }
}

public sealed class CreateTodoEndpoint(ISender sender) : Endpoint<CreateTodoRequest, TodoResponse>
{
    public override void Configure()
    {
        Post("/api/todos");
        Tags("Todos");
    }

    public override async Task HandleAsync(CreateTodoRequest request, CancellationToken ct)
    {
        var result =
            await sender.Send(
                new CreateTodoCommand(request.Description, request.Priority, request.DueDate, request.Labels),
                ct);
        await Send.ResultAsync(result.ToHttp(HttpContext, todo => Results.Created($"/api/todos/{todo.Id}", todo)));
    }
}