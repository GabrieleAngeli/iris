namespace Iris.Domain.Common;

/// <summary>
/// Marks an entity as an aggregate root — the only entry point through which
/// its aggregate may be loaded, mutated and persisted.
/// </summary>
public interface IAggregateRoot;
