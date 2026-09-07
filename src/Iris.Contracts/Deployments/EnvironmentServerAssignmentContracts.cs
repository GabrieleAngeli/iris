namespace Iris.Contracts.Deployments;

/// <summary>Body of <c>POST /deployments/contexts/{contextId}/server-assignments</c>.</summary>
public sealed record AssignServerToEnvironmentRequest(Guid ServerNodeId, string? Notes = null);

public sealed record EnvironmentServerAssignmentResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid CustomerContextId,
    string CustomerContextName,
    string Environment,
    Guid ServerNodeId,
    string ServerName,
    string ServerHostingType,
    string? Notes,
    DateTimeOffset CreatedAtUtc);
