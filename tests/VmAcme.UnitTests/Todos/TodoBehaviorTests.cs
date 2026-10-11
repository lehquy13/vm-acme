using VmAcme.Behaviors;
using VmAcme.Common;
using Unit = VmAcme.Common.Unit;

namespace VmAcme.Todos;

public sealed class TodoBehaviorTests
{
    [Fact]
    public void Completing_twice_returns_conflict_and_preserves_timestamp()
    {
        var now = new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
        var todo = TodoItem.Create(Guid.NewGuid(), "Complete", Priority.High, null, [], now);
        Assert.True(todo.Complete(now.AddHours(1)).IsSuccess);
        var result = todo.Complete(now.AddHours(2));
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(now.AddHours(1), todo.CompletedAt);
    }

    [Fact]
    public async Task Validation_returns_result_and_never_invokes_handler()
    {
        var behavior =
            new ResultValidationBehavior<CreateTodoCommand, Result<TodoResponse>>([
                new CreateTodoValidator(TimeProvider.System)
            ]);
        var called = false;
        var result = await behavior.Handle(new CreateTodoCommand(" ", Priority.Normal, null, []), _ =>
        {
            called = true;
            throw new InvalidOperationException("Invalid requests must not reach the handler.");
        }, CancellationToken.None);
        Assert.False(called);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains("Description", result.Error.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Result_failure_is_recorded_as_failed_request_metric()
    {
        var metrics = new RequestMetrics();
        var behavior = new MetricsBehavior<DeleteTodoCommand, Result<Unit>>(metrics);
        await behavior.Handle(new DeleteTodoCommand(Guid.NewGuid()),
            _ => Task.FromResult(Result<Unit>.Failure(new Error("Todos.NotFound", "Missing", ErrorType.NotFound))),
            CancellationToken.None);
        Assert.Contains("vm_acme_mediatr_request_failures_total{request=\"DeleteTodoCommand\"} 1",
            metrics.ToPrometheusText());
    }
}