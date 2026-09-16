using Iris.Domain.Deployments;

namespace Iris.Application.Abstractions;

public interface IEnvironmentServerAssignmentRepository
{
    /// <summary>Every assignment, across every customer/context (read-only).</summary>
    Task<IReadOnlyList<EnvironmentServerAssignment>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<EnvironmentServerAssignment?> GetAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Every assignment for one server — used to resolve which <c>CustomerContext</c>(s)
    /// a server belongs to (e.g. for AWX context-name-based Job Template resolution).</summary>
    Task<IReadOnlyList<EnvironmentServerAssignment>> GetForServerAsync(Guid serverNodeId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid customerContextId, Guid serverNodeId, CancellationToken cancellationToken = default);

    Task AddAsync(EnvironmentServerAssignment assignment, CancellationToken cancellationToken = default);

    void Remove(EnvironmentServerAssignment assignment);
}
