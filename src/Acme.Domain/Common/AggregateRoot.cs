namespace Acme.Domain.Common;

public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
}
