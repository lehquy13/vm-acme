using Acme.Domain.Common;

namespace Acme.Domain.WorkItems;

public sealed class WorkItem : AggregateRoot<WorkItemId>
{
    private readonly object _completionLock = new();

    private WorkItem() { }

    public WorkItemTitle Title { get; private set; } = null!;

    public WorkItemStatus Status { get; private set; }

    public static WorkItem Create(WorkItemTitle title)
    {
        ArgumentNullException.ThrowIfNull(title);

        return new WorkItem
        {
            Id = WorkItemId.New(),
            Title = title,
            Status = WorkItemStatus.Open
        };
    }

    public bool Complete()
    {
        lock (_completionLock)
        {
            if (Status is WorkItemStatus.Completed)
            {
                return false;
            }

            Status = WorkItemStatus.Completed;
            return true;
        }
    }
}
