using Template.Domain.WorkItems;

namespace Template.UnitTests.WorkItems;

public sealed class WorkItemTests
{
    [Fact]
    public void Create_sets_title_and_open_status()
    {
        var title = new WorkItemTitle("  Write tests  ");

        var workItem = WorkItem.Create(title);

        Assert.NotEqual(default, workItem.Id);
        Assert.Equal("Write tests", workItem.Title.Value);
        Assert.Equal(WorkItemStatus.Open, workItem.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Title_rejects_empty_or_whitespace(string title)
    {
        Assert.Throws<ArgumentException>(() => new WorkItemTitle(title));
    }

    [Fact]
    public void Title_rejects_more_than_200_characters()
    {
        Assert.Throws<ArgumentException>(() => new WorkItemTitle(new string('x', 201)));
    }

    [Fact]
    public void Complete_changes_open_item_once()
    {
        var workItem = WorkItem.Create(new WorkItemTitle("Ship template"));

        Assert.True(workItem.Complete());
        Assert.False(workItem.Complete());
        Assert.Equal(WorkItemStatus.Completed, workItem.Status);
    }

    [Fact]
    public async Task Complete_succeeds_only_once_for_concurrent_calls()
    {
        var workItem = WorkItem.Create(new WorkItemTitle("Complete concurrently"));
        using var start = new Barrier(32);
        var calls = Enumerable.Range(0, 32)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait();
                    return workItem.Complete();
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        var results = await Task.WhenAll(calls);

        Assert.Single(results, completed => completed);
    }
}
