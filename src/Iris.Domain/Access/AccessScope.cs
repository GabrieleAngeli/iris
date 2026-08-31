namespace Iris.Domain.Access;

/// <summary>
/// Value object describing the portion of the platform a role assignment grants
/// against, or the target a permission check is evaluated for.
/// </summary>
public sealed class AccessScope : IEquatable<AccessScope>
{
    // Parameterless ctor for the persistence layer (complex-type materialization).
    private AccessScope()
    {
    }

    private AccessScope(ScopeType type, Guid? customerId, Guid? contextId)
    {
        Type = type;
        CustomerId = customerId;
        ContextId = contextId;
    }

    public ScopeType Type { get; private set; }

    public Guid? CustomerId { get; private set; }

    public Guid? ContextId { get; private set; }

    public static AccessScope Global() => new(ScopeType.Global, null, null);

    public static AccessScope ForCustomer(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required for a customer scope.", nameof(customerId));
        }

        return new AccessScope(ScopeType.Customer, customerId, null);
    }

    public static AccessScope ForContext(Guid customerId, Guid contextId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required for a context scope.", nameof(customerId));
        }

        if (contextId == Guid.Empty)
        {
            throw new ArgumentException("Context id is required for a context scope.", nameof(contextId));
        }

        return new AccessScope(ScopeType.Context, customerId, contextId);
    }

    /// <summary>
    /// True when a role granted at <c>this</c> scope also applies to <paramref name="target"/>.
    /// Global covers everything; a customer scope covers that customer and every context under it;
    /// a context scope covers only itself.
    /// </summary>
    public bool Covers(AccessScope target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return Type switch
        {
            ScopeType.Global => true,
            ScopeType.Customer => target.CustomerId == CustomerId,
            ScopeType.Context => target.CustomerId == CustomerId && target.ContextId == ContextId,
            _ => false,
        };
    }

    public bool Equals(AccessScope? other) =>
        other is not null && Type == other.Type && CustomerId == other.CustomerId && ContextId == other.ContextId;

    public override bool Equals(object? obj) => Equals(obj as AccessScope);

    public override int GetHashCode() => HashCode.Combine(Type, CustomerId, ContextId);

    public override string ToString() => Type switch
    {
        ScopeType.Global => "global",
        ScopeType.Customer => $"customer:{CustomerId}",
        ScopeType.Context => $"context:{CustomerId}/{ContextId}",
        _ => "unknown",
    };
}
