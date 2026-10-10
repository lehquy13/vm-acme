using Template.Domain.WorkItems;

namespace Template.Application.WorkItems;

public sealed class CompleteWorkItem(IWorkItemRepository repository)
{
    public async Task<WorkItemDto?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var workItem = await repository.GetAsync(new WorkItemId(id), cancellationToken);
        if (workItem is null)
        {
            return null;
        }

        if (!workItem.Complete())
        {
            throw new InvalidOperationException("Work item is already completed.");
        }

        await repository.UpdateAsync(workItem, cancellationToken);
        return WorkItemDto.From(workItem);
    }
}
