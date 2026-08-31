namespace Iris.Domain.Access;

/// <summary>Breadth at which a <see cref="RoleAssignment"/> applies.</summary>
public enum ScopeType
{
    /// <summary>The whole platform, every customer and context.</summary>
    Global = 0,

    /// <summary>A single customer and all of its contexts.</summary>
    Customer = 1,

    /// <summary>A single context of a single customer.</summary>
    Context = 2,
}
