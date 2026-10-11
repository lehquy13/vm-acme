using FastEndpoints;
using MediatR;
using VmAcme.Infrastructure;
using VmAcme.Todos;

namespace VmAcme.Endpoints.Todos;

public sealed class TodoIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetTodoEndpoint(ISender sender) : Endpoint<TodoIdRequest, TodoResponse>
{
    public override void Configure()
    {
        Get("/api/todos/{id}");
        Tags("Todos");
    }

    public override async Task HandleAsync(TodoIdRequest request, CancellationToken ct) =>
        await Send.ResultAsync(
            (await sender.Send(new GetTodoQuery(request.Id), ct)).ToHttp(HttpContext));
}