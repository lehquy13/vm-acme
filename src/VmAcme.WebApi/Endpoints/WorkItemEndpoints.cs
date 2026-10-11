using VmAcme.WorkItems;
using FluentValidation;
using MediatR;

namespace VmAcme.Endpoints;

public static class WorkItemEndpoints
{
    public static IEndpointRouteBuilder MapWorkItemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/work-items").WithTags("WorkItems");
        group.RequireAuthorization();

        group.MapPost("/", CreateAsync).WithName("CreateWorkItem");
        group.MapGet("/", ListAsync).WithName("ListWorkItems");
        group.MapPost("/{id:guid}/complete", CompleteAsync).WithName("CompleteWorkItem");

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateWorkItemRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var workItem = await sender.Send(new CreateWorkItemCommand(request.Title), cancellationToken);
            return Results.Created($"/api/work-items/{workItem.Id}", workItem);
        }
        catch (ValidationException exception)
        {
            return Results.BadRequest(new { errors = exception.Errors.Select(error => error.ErrorMessage).Distinct() });
        }
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var workItems = await sender.Send(new ListWorkItemsQuery(), cancellationToken);
        return Results.Ok(workItems);
    }

    private static async Task<IResult> CompleteAsync(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var workItem = await sender.Send(new CompleteWorkItemCommand(id), cancellationToken);
            return workItem is null ? Results.NotFound() : Results.Ok(workItem);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return Results.BadRequest(new { errors = exception.Errors.Select(error => error.ErrorMessage).Distinct() });
        }
    }

    public sealed record CreateWorkItemRequest(string? Title);
}
