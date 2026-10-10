using Template.Application.WorkItems;

namespace Template.WebApi.Endpoints;

public static class WorkItemEndpoints
{
    public static IEndpointRouteBuilder MapWorkItemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/work-items").WithTags("WorkItems");

        group.MapPost("/", CreateAsync).WithName("CreateWorkItem");
        group.MapGet("/", ListAsync).WithName("ListWorkItems");
        group.MapPost("/{id:guid}/complete", CompleteAsync).WithName("CompleteWorkItem");

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateWorkItemRequest request,
        CreateWorkItem useCase,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new { error = "Title is required." });
        }

        try
        {
            var workItem = await useCase.HandleAsync(request.Title, cancellationToken);
            return Results.Created($"/api/work-items/{workItem.Id}", workItem);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> ListAsync(
        ListWorkItems useCase,
        CancellationToken cancellationToken)
    {
        var workItems = await useCase.HandleAsync(cancellationToken);
        return Results.Ok(workItems);
    }

    private static async Task<IResult> CompleteAsync(
        Guid id,
        CompleteWorkItem useCase,
        CancellationToken cancellationToken)
    {
        try
        {
            var workItem = await useCase.HandleAsync(id, cancellationToken);
            return workItem is null ? Results.NotFound() : Results.Ok(workItem);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    public sealed record CreateWorkItemRequest(string? Title);
}
