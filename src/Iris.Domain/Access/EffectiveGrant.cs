namespace Iris.Domain.Access;

/// <summary>
/// A user's permissions contributed by one role assignment, flattened for the
/// <see cref="PermissionResolver"/> (assignment scope + the role's permission codes).
/// </summary>
/// <param name="Scope">Scope the assignment was granted at.</param>
/// <param name="Permissions">Permission codes carried by the assigned role.</param>
public sealed record EffectiveGrant(AccessScope Scope, IReadOnlyCollection<string> Permissions);
