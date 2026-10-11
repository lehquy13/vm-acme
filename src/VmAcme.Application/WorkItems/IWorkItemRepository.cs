namespace VmAcme.WorkItems;

public interface IWorkItemRepository
{
    Task AddAsync(WorkItem workItem, CancellationToken cancellationToken);

    Task<WorkItem?> GetAsync(WorkItemId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItem>> ListAsync(CancellationToken cancellationToken);

    Task UpdateAsync(WorkItem workItem, CancellationToken cancellationToken);
}