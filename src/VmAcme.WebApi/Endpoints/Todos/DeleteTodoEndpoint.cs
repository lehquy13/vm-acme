using FastEndpoints;
using MediatR;
using VmAcme.Infrastructure;
using VmAcme.Todos;

namespace VmAcme.Endpoints.Todos;

public sealed class DeleteTodoEndpoint(ISender sender) : Endpoint<TodoIdRequest>
{
    public override void Configure()
    {
        Delete("/api/todos/{id}");
        Tags("Todos");
    }

    public override async Task HandleAsync(TodoIdRequest request, CancellationToken ct) =>
        await Send.ResultAsync(
            (await sender.Send(new DeleteTodoCommand(request.Id), ct)).ToHttp(HttpContext,
                _ => Results.NoContent()));
}