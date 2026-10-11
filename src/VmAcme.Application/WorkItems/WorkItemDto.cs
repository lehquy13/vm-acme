namespace VmAcme.WorkItems;

public sealed record WorkItemDto(Guid Id, string Title, string Status)
{
    public static WorkItemDto From(WorkItem workItem) =>
        new(workItem.Id.Value, workItem.Title.Value, workItem.Status.ToString());
}