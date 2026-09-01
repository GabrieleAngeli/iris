namespace Iris.Contracts.Governance;

/// <summary>Body of <c>POST /customers</c>.</summary>
public sealed record CreateCustomerRequest(string Key, string Name);

/// <summary>Body of <c>POST /customers/{customerId}/contexts</c>.</summary>
public sealed record AddContextRequest(string Name, string Kind);

/// <summary>Body of <c>PUT /governance/users/{userId}</c>.</summary>
public sealed record UpdateUserRequest(string Email, string DisplayName, bool IsActive);

/// <summary>Body of <c>POST /users/{userId}/assignments</c>.</summary>
public sealed record AssignRoleRequest(string RoleKey, string ScopeType, Guid? CustomerId, Guid? ContextId);

/// <summary>Body of <c>POST /governance/users</c> — pre-provisions a user ahead of their first sign-in.</summary>
public sealed record CreateUserRequest(string Email, string DisplayName);
