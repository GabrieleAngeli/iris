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

    /// <summary>The matching "context" name in the AWX automation repo (e.g. <c>cloud_02-trial</c>),
    /// if this environment has one — used to resolve Job Templates by name
    /// (<c>"{AwxContextName}-facts"</c>, <c>"{AwxContextName}-site"</c>) instead of a single
    /// global id, since the repo declares Job Templates per context. Null until set; not every
    /// context needs one (only those actually represented in the awx repo).</summary>
    public string? AwxContextName { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void SetAwxContextName(string? awxContextName) =>
        AwxContextName = string.IsNullOrWhiteSpace(awxContextName) ? null : awxContextName.Trim();

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
