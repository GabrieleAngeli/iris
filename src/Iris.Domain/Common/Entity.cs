namespace Iris.Domain.Common;

/// <summary>
/// Base class for domain entities identified by a strongly-typed key.
/// Identity-based equality: two entities are equal when their ids are equal.
/// </summary>
public abstract class Entity<TId>
    where TId : notnull
{
    protected Entity(TId id) => Id = id;

    public TId Id { get; }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && GetType() == other.GetType() && Id.Equals(other.Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
