using Iris.Domain.Access;

namespace Iris.Application.Access;

/// <summary>One resolved role assignment: the scope it applies at and the permissions it carries.</summary>
public sealed record AssignmentView(
    Guid RoleId,
    string RoleKey,
    string RoleName,
    AccessScope Scope,
    IReadOnlyCollection<string> Permissions);

/// <summary>
/// Everything needed to answer authorization questions for one user, assembled
/// once per request: their internal identity plus every role assignment flattened
/// with its permissions.
/// </summary>
public sealed record UserAccessSnapshot(
    Guid UserId,
    string ExternalId,
    string Email,
    string DisplayName,
    IReadOnlyList<AssignmentView> Assignments)
{
    /// <summary>Grants in the shape the domain <see cref="PermissionResolver"/> consumes.</summary>
    public IReadOnlyList<EffectiveGrant> ToGrants() =>
        Assignments.Select(a => new EffectiveGrant(a.Scope, a.Permissions)).ToList();

    /// <summary>Effective permission codes for <paramref name="target"/>.</summary>
    public IReadOnlySet<string> EffectivePermissions(AccessScope target) =>
        PermissionResolver.Resolve(ToGrants(), target);

    /// <summary>True when the user may see the customer <paramref name="customerId"/> at all.</summary>
    public bool CanSeeCustomer(Guid customerId) =>
        Assignments.Any(a => a.Scope.Type == ScopeType.Global || a.Scope.CustomerId == customerId);

    /// <summary>True when the user may see the specific context.</summary>
    public bool CanSeeContext(Guid customerId, Guid contextId) =>
        Assignments.Any(a =>
            a.Scope.Type == ScopeType.Global ||
            (a.Scope.Type == ScopeType.Customer && a.Scope.CustomerId == customerId) ||
            (a.Scope.Type == ScopeType.Context && a.Scope.CustomerId == customerId && a.Scope.ContextId == contextId));
}
