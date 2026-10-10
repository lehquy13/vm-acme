using System.Collections.Concurrent;
using Template.Application.WorkItems;
using Template.Domain.WorkItems;

namespace Template.Infrastructure.Persistence;

public sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private readonly ConcurrentDictionary<Guid, WorkItem> _items = new();

    public Task AddAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_items.TryAdd(workItem.Id.Value, workItem))
        {
            throw new InvalidOperationException($"Work item '{workItem.Id}' already exists.");
        }

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
        IReadOnlyList<WorkItem> items = _items.Values
            .OrderBy(item => item.Title.Value, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult(items);
    }

    public Task UpdateAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_items.ContainsKey(workItem.Id.Value))
        {
            throw new InvalidOperationException($"Work item '{workItem.Id}' does not exist.");
        }

        _items[workItem.Id.Value] = workItem;
        return Task.CompletedTask;
    }
}
