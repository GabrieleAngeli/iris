namespace Iris.Contracts.Access;

/// <summary>One role the caller holds, and where it applies.</summary>
public sealed record RoleAssignmentDto(
    string RoleKey,
    string RoleName,
    string ScopeType,
    Guid? CustomerId,
    Guid? ContextId,
    IReadOnlyList<string> Permissions);

/// <summary>
/// The caller's identity and authorization state, optionally evaluated against a
/// requested customer/context scope. Returned by <c>GET /me</c>.
/// </summary>
public sealed record MeResponse(
    Guid UserId,
    string ExternalId,
    string Email,
    string DisplayName,
    string EvaluatedScope,
    IReadOnlyList<string> EffectivePermissions,
    IReadOnlyList<RoleAssignmentDto> Assignments,
    bool PasswordSetupPending = false);
