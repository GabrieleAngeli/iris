using Iris.Domain.Common;

namespace Iris.Domain.Deployments;

/// <summary>
/// A server assigned to host workloads for one customer's environment — the answer to "which
/// servers does this environment run on", independent of whether anything has been installed on
/// it yet. Composing a deployment is meant to work top-down: assign the servers an environment
/// uses first, then per server decide which applications and installation mode run there
/// (<c>ApplicationInstallation</c>). Its own aggregate: a planning/topology fact, not a child of
/// <c>CustomerContext</c> or <c>ServerNode</c> — either can be read without loading the other.
/// </summary>
public sealed class EnvironmentServerAssignment : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    // For the persistence layer.
    private EnvironmentServerAssignment()
        : base(Guid.Empty)
    {
    }

    public EnvironmentServerAssignment(Guid id, Guid customerContextId, Guid serverNodeId, string? notes)
        : base(id)
    {
        CustomerContextId = customerContextId;
        ServerNodeId = serverNodeId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid CustomerContextId { get; private set; }

    public Guid ServerNodeId { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
