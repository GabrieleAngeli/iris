namespace Iris.Contracts.Access;

public sealed record UserAssignmentDto(
    Guid AssignmentId,
    string RoleKey,
    string RoleName,
    string ScopeType,
    Guid? CustomerId,
    Guid? ContextId);

public sealed record UserResponse(
    Guid Id,
    string ExternalId,
    string Email,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<UserAssignmentDto> Assignments);

public sealed record AssignmentResponse(
    Guid Id,
    Guid UserId,
    string RoleKey,
    string ScopeType,
    Guid? CustomerId,
    Guid? ContextId);
