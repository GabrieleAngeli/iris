namespace Iris.Contracts.Governance;

/// <summary>Body of <c>POST /customers</c>.</summary>
public sealed record CreateCustomerRequest(string Key, string Name);

/// <summary>Body of <c>POST /customers/{customerId}/contexts</c>.</summary>
public sealed record AddContextRequest(string Name, string Kind);

/// <summary>Body of <c>POST /users/{userId}/assignments</c>.</summary>
public sealed record AssignRoleRequest(string RoleKey, string ScopeType, Guid? CustomerId, Guid? ContextId);
