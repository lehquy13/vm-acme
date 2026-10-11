using FastEndpoints;
using MediatR;
using VmAcme.Infrastructure;
using VmAcme.Todos;

namespace VmAcme.Endpoints.Todos;

public sealed class CompleteTodoEndpoint(ISender sender) : Endpoint<TodoIdRequest, TodoResponse>
{
    public override void Configure()
    {
        Post("/api/todos/{id}/complete");
        Tags("Todos");
    }

    public override async Task HandleAsync(TodoIdRequest request, CancellationToken ct) =>
        await Send.ResultAsync(
            (await sender.Send(new CompleteTodoCommand(request.Id), ct)).ToHttp(HttpContext));
}