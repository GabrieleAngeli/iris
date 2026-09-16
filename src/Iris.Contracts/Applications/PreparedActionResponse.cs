namespace Iris.Contracts.Applications;

/// <summary>
/// One Prepare → Review → Execute action for an installation. <see cref="Plan"/>/<see cref="Validation"/>
/// are the frozen snapshot the operator reviewed at Prepare time — not recomputed live — so this
/// stays an accurate record of what was approved even if the installation changes afterwards.
/// <see cref="Run"/> is populated once <see cref="Status"/> is <c>Executed</c>.
/// </summary>
public sealed record PreparedActionResponse(
    Guid Id,
    Guid InstallationId,
    string Status,
    ApplicationInstallationAnsiblePlanResponse Plan,
    ApplicationInstallationValidationResponse Validation,
    ApplicationInstallationAwxLaunchRequest RequestedLaunchOptions,
    Guid? InstallationRunId,
    InstallationRunResponse? Run,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExecutedAtUtc,
    DateTimeOffset? CanceledAtUtc,
    string? CancelReason);

/// <summary>One row in the Actions list — <see cref="EffectiveStatus"/> is <c>Prepared</c>/<c>Canceled</c>
/// for actions not yet executed, or the linked <see cref="InstallationRunResponse.Status"/> once executed,
/// so the list reads as one continuous status arc rather than two separate entities.</summary>
public sealed record ActionSummaryResponse(
    Guid Id,
    Guid InstallationId,
    string InstallationName,
    string ApplicationSlug,
    string ApplicationVersion,
    string CustomerName,
    string Environment,
    string ServerName,
    string EffectiveStatus,
    DateTimeOffset CreatedAtUtc,
    Guid? InstallationRunId);

public sealed record CancelPreparedActionRequest(string? Reason = null);
