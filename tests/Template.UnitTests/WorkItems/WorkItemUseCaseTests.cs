using System.Collections.Concurrent;
using Template.Application.WorkItems;
using Template.Domain.WorkItems;

namespace Template.UnitTests.WorkItems;

public sealed class WorkItemUseCaseTests
{
    [Fact]
    public async Task Create_persists_and_returns_new_work_item()
    {
        var repository = new FakeWorkItemRepository();
        var useCase = new CreateWorkItem(repository);

        var result = await useCase.HandleAsync("Build template", CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Build template", result.Title);
        Assert.Equal("Open", result.Status);
        Assert.NotNull(await repository.GetAsync(new WorkItemId(result.Id), CancellationToken.None));
    }

    [Fact]
    public async Task List_returns_persisted_work_items()
    {
        var repository = new FakeWorkItemRepository();
        var create = new CreateWorkItem(repository);
        await create.HandleAsync("First", CancellationToken.None);
        await create.HandleAsync("Second", CancellationToken.None);

        var result = await new ListWorkItems(repository).HandleAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Create_rejects_invalid_title_before_persisting()
    {
        var repository = new FakeWorkItemRepository();
        var useCase = new CreateWorkItem(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.HandleAsync(" ", CancellationToken.None));
        Assert.Empty(await repository.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Complete_updates_open_work_item()
    {
        var repository = new FakeWorkItemRepository();
        var created = await new CreateWorkItem(repository).HandleAsync("Complete me", CancellationToken.None);

        var result = await new CompleteWorkItem(repository).HandleAsync(created.Id, CancellationToken.None);

        Assert.Equal("Completed", result?.Status);
    }

    [Fact]
    public async Task Complete_returns_null_for_missing_work_item()
    {
        var useCase = new CompleteWorkItem(new FakeWorkItemRepository());

        var result = await useCase.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Complete_rejects_already_completed_work_item()
    {
        var repository = new FakeWorkItemRepository();
        var created = await new CreateWorkItem(repository).HandleAsync("Complete me twice", CancellationToken.None);
        var useCase = new CompleteWorkItem(repository);
        await useCase.HandleAsync(created.Id, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.HandleAsync(created.Id, CancellationToken.None));
    }

    private sealed class FakeWorkItemRepository : IWorkItemRepository
    {
        private readonly ConcurrentDictionary<Guid, WorkItem> _items = new();

        public Task AddAsync(WorkItem workItem, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _items[workItem.Id.Value] = workItem;
            return Task.CompletedTask;
        }

        public Task<WorkItem?> GetAsync(WorkItemId id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _items.TryGetValue(id.Value, out var workItem);
            return Task.FromResult(workItem);
        }

        public Task<IReadOnlyList<WorkItem>> ListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<WorkItem>>(_items.Values.ToArray());
        }

        public Task UpdateAsync(WorkItem workItem, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _items[workItem.Id.Value] = workItem;
            return Task.CompletedTask;
        }
    }
}
