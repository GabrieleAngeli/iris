using Iris.Domain.Common;

namespace Iris.Domain.Applications;

/// <summary>
/// Lifecycle of a <see cref="PreparedAction"/> itself — not the AWX job it may lead to. Once
/// <see cref="Executed"/>, the actual execution status lives on the linked
/// <see cref="InstallationRun"/> (via <see cref="PreparedAction.InstallationRunId"/>); this entity
/// does not track it further. <see cref="Canceled"/> and <see cref="Executed"/> are terminal.
/// </summary>
public enum PreparedActionStatus
{
    Prepared = 0,
    Canceled = 1,
    Executed = 2,
}

/// <summary>
/// An explicit, reviewable plan for deploying an <see cref="ApplicationInstallation"/>, captured
/// before anything is sent to AWX: a frozen snapshot of the Ansible plan and validation checks the
/// operator reviewed, plus the launch options they will confirm. Its own aggregate (mirrors
/// <see cref="InstallationRun"/>'s shape deliberately) — Prepare only ever reads and snapshots;
/// nothing runs until <see cref="MarkExecuted"/>, which happens after
/// <c>LaunchApplicationInstallationAwxJobHandler</c> has already created the real
/// <see cref="InstallationRun"/>. This keeps "what the operator reviewed and approved" as a
/// permanent, unmutated audit record even if the installation changes afterwards.
/// </summary>
public sealed class PreparedAction : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    // For the persistence layer.
    private PreparedAction()
        : base(Guid.Empty)
    {
        PlanSnapshotJson = string.Empty;
        ValidationSnapshotJson = string.Empty;
    }

    public PreparedAction(
        Guid id,
        Guid applicationInstallationId,
        string planSnapshotJson,
        string validationSnapshotJson,
        int? requestedJobTemplateId,
        string? requestedInventory,
        string? requestedLimit,
        bool requestedCheckMode)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planSnapshotJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(validationSnapshotJson);

        ApplicationInstallationId = applicationInstallationId;
        Status = PreparedActionStatus.Prepared;
        PlanSnapshotJson = planSnapshotJson;
        ValidationSnapshotJson = validationSnapshotJson;
        RequestedJobTemplateId = requestedJobTemplateId;
        RequestedInventory = string.IsNullOrWhiteSpace(requestedInventory) ? null : requestedInventory.Trim();
        RequestedLimit = string.IsNullOrWhiteSpace(requestedLimit) ? null : requestedLimit.Trim();
        RequestedCheckMode = requestedCheckMode;
    }

    public Guid ApplicationInstallationId { get; private set; }

    public PreparedActionStatus Status { get; private set; }

    /// <summary>The <c>ApplicationInstallationAnsiblePlanResponse</c> the operator reviewed, frozen at Prepare time.</summary>
    public string PlanSnapshotJson { get; private set; }

    /// <summary>The <c>ApplicationInstallationValidationResponse</c> the operator reviewed, frozen at Prepare time.</summary>
    public string ValidationSnapshotJson { get; private set; }

    public int? RequestedJobTemplateId { get; private set; }

    public string? RequestedInventory { get; private set; }

    public string? RequestedLimit { get; private set; }

    public bool RequestedCheckMode { get; private set; }

    /// <summary>The <see cref="InstallationRun"/> this action produced, once executed.</summary>
    public Guid? InstallationRunId { get; private set; }

    public string? CancelReason { get; private set; }

    public DateTimeOffset? ExecutedAtUtc { get; private set; }

    public DateTimeOffset? CanceledAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public bool IsTerminal => Status is PreparedActionStatus.Canceled or PreparedActionStatus.Executed;

    /// <summary>The operator declined to proceed. No-op if already terminal — mirrors <see cref="InstallationRun"/>'s flapping guard.</summary>
    public void Cancel(string? reason, DateTimeOffset nowUtc)
    {
        if (IsTerminal)
        {
            return;
        }

        CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Status = PreparedActionStatus.Canceled;
        CanceledAtUtc = nowUtc;
    }

    /// <summary>The operator confirmed and AWX has already been launched — <paramref name="installationRunId"/> is that run's id.</summary>
    public void MarkExecuted(Guid installationRunId, DateTimeOffset nowUtc)
    {
        if (Status != PreparedActionStatus.Prepared)
        {
            throw new InvalidOperationException($"Only a Prepared action can be executed (current status: {Status}).");
        }

        InstallationRunId = installationRunId;
        Status = PreparedActionStatus.Executed;
        ExecutedAtUtc = nowUtc;
    }
}
