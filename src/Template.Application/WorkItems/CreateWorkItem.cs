using Template.Domain.WorkItems;

namespace Template.Application.WorkItems;

public sealed class CreateWorkItem(IWorkItemRepository repository)
{
    public async Task<WorkItemDto> HandleAsync(string title, CancellationToken cancellationToken)
    {
        var workItem = WorkItem.Create(new WorkItemTitle(title));
        await repository.AddAsync(workItem, cancellationToken);
        return WorkItemDto.From(workItem);
    }
}
