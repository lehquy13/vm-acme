namespace VmAcme.WorkItems;

public sealed record WorkItemTitle
{
    public const int MaxLength = 200;

    public string Value { get; }

    public WorkItemTitle(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim();
        if (normalized.Length > MaxLength)
        {
            throw new ArgumentException($"Title cannot exceed {MaxLength} characters.", nameof(value));
        }

        Value = normalized;
    }

    public override string ToString() => Value;
}