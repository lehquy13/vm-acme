using FastEndpoints;
using MediatR;
using VmAcme.Infrastructure;
using VmAcme.Todos;

namespace VmAcme.Endpoints.Todos;

public sealed class ListTodosEndpoint(ISender sender) : EndpointWithoutRequest<IReadOnlyList<TodoResponse>>
{
    public override void Configure()
    {
        Get("/api/todos");
        Tags("Todos");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.ResultAsync((await sender.Send(new ListTodosQuery(), ct)).ToHttp(HttpContext));
}