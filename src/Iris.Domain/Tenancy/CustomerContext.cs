using Iris.Domain.Common;

namespace Iris.Domain.Tenancy;

/// <summary>
/// An environment owned by a <see cref="Customer"/> (e.g. their Test, Staging or
/// Production). Deployments and domain bindings are ultimately resolved per context.
/// </summary>
public sealed class CustomerContext : Entity<Guid>, IAuditableEntity
{
    // For the persistence layer.
    private CustomerContext()
        : base(Guid.Empty)
    {
        Name = string.Empty;
    }

    internal CustomerContext(Guid id, Guid customerId, string name, ContextKind kind)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        CustomerId = customerId;
        Name = name.Trim();
        Kind = kind;
        IsActive = true;
    }

    public Guid CustomerId { get; private set; }

    public string Name { get; private set; }

    public ContextKind Kind { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
