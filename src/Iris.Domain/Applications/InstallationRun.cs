using Iris.Domain.Common;

namespace Iris.Domain.Applications;

/// <summary>How an <see cref="InstallationRun"/> was carried out. Only AWX today.</summary>
public enum InstallationRunKind
{
    AwxJob = 0,
}

/// <summary>
/// Lifecycle of an <see cref="InstallationRun"/>. <see cref="Succeeded"/>, <see cref="Failed"/>
/// and <see cref="Canceled"/> are terminal.
/// </summary>
public enum InstallationRunStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Canceled = 4,
}

/// <summary>
/// One recorded attempt to deploy an <see cref="ApplicationInstallation"/> through an external
/// executor (AWX today). Iris does not run anything itself — it hands the plan to AWX/Ansible and
/// keeps this row as the audit trail: which external job, what status, what variables were
/// submitted. Its own aggregate, referenced by <see cref="ApplicationInstallationId"/> — Iris
/// never mutates the installation from here.
/// </summary>
public sealed class InstallationRun : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    // For the persistence layer.
    private InstallationRun()
        : base(Guid.Empty)
    {
    }

    public InstallationRun(
        Guid id,
        Guid applicationInstallationId,
        InstallationRunKind kind,
        string? submittedVariablesJson)
        : base(id)
    {
        ApplicationInstallationId = applicationInstallationId;
        Kind = kind;
        Status = InstallationRunStatus.Pending;
        SubmittedVariablesJson = string.IsNullOrWhiteSpace(submittedVariablesJson) ? null : submittedVariablesJson;
    }

    public Guid ApplicationInstallationId { get; private set; }

    public InstallationRunKind Kind { get; private set; }

    public InstallationRunStatus Status { get; private set; }

    /// <summary>The executor's own job identifier (AWX job id), once accepted.</summary>
    public string? ExternalJobId { get; private set; }

    /// <summary>A link to the run in the executor's UI/API, if it returned one.</summary>
    public string? ExternalUrl { get; private set; }

    public string? SubmittedVariablesJson { get; private set; }

    public string? Message { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public bool IsTerminal => IsTerminalStatus(Status);

    /// <summary>The executor accepted the plan and returned a job handle.</summary>
    public void MarkSubmitted(
        string? externalJobId,
        string? externalUrl,
        InstallationRunStatus status,
        string? message,
        DateTimeOffset nowUtc)
    {
        ExternalJobId = string.IsNullOrWhiteSpace(externalJobId) ? null : externalJobId.Trim();
        ExternalUrl = string.IsNullOrWhiteSpace(externalUrl) ? null : externalUrl.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        Apply(status, nowUtc);
    }

    /// <summary>Refreshes the status from a later poll of the executor.</summary>
    public void UpdateStatus(InstallationRunStatus status, string? message, DateTimeOffset nowUtc)
    {
        if (IsTerminal)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            Message = message.Trim();
        }

        Apply(status, nowUtc);
    }

    /// <summary>The executor rejected the plan, or Iris could not reach it.</summary>
    public void MarkFailed(string message, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (IsTerminal)
        {
            return;
        }

        Message = message.Trim();
        Apply(InstallationRunStatus.Failed, nowUtc);
    }

    private void Apply(InstallationRunStatus status, DateTimeOffset nowUtc)
    {
        Status = status;
        if (IsTerminalStatus(status))
        {
            CompletedAtUtc ??= nowUtc;
        }
    }

    private static bool IsTerminalStatus(InstallationRunStatus status) =>
        status is InstallationRunStatus.Succeeded or InstallationRunStatus.Failed or InstallationRunStatus.Canceled;
}
