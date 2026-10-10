namespace Template.Application.WorkItems;

public sealed class ListWorkItems(IWorkItemRepository repository)
{
    public async Task<IReadOnlyList<WorkItemDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var items = await repository.ListAsync(cancellationToken);
        return items.Select(WorkItemDto.From).ToArray();
    }
}
