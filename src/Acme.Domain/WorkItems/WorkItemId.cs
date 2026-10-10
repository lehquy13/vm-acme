namespace Acme.Domain.WorkItems;

public readonly record struct WorkItemId(Guid Value)
{
    public static WorkItemId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
