using Iris.Domain.Common;

namespace Iris.Domain.Tenancy;

/// <summary>
/// A tenant of Iris. Owns one or more <see cref="CustomerContext"/> environments.
/// Users only ever see the customers and contexts their role assignments cover.
/// </summary>
public sealed class Customer : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    private readonly List<CustomerContext> _contexts = [];

    // For the persistence layer.
    private Customer()
        : base(Guid.Empty)
    {
        Key = string.Empty;
        Name = string.Empty;
    }

    public Customer(Guid id, string key, string name)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Key = key.Trim().ToLowerInvariant();
        Name = name.Trim();
        IsActive = true;
    }

    public string Key { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<CustomerContext> Contexts => _contexts.AsReadOnly();

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public CustomerContext AddContext(Guid contextId, string name, ContextKind kind)
    {
        if (_contexts.Any(c => string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Context '{name}' already exists for customer '{Key}'.");
        }

        var context = new CustomerContext(contextId, Id, name, kind);
        _contexts.Add(context);
        return context;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
