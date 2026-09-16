using System.Text.Json;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

internal static class PreparedActionMapping
{
    public static PreparedActionResponse ToResponse(
        this PreparedAction action,
        ApplicationInstallationAnsiblePlanResponse plan,
        ApplicationInstallationValidationResponse validation,
        InstallationRunResponse? run) => new(
            action.Id,
            action.ApplicationInstallationId,
            action.Status.ToString(),
            plan,
            validation,
            new ApplicationInstallationAwxLaunchRequest(
                action.RequestedJobTemplateId, action.RequestedInventory, action.RequestedLimit, action.RequestedCheckMode),
            action.InstallationRunId,
            run,
            action.CreatedAtUtc,
            action.ExecutedAtUtc,
            action.CanceledAtUtc,
            action.CancelReason);

    public static ApplicationInstallationAnsiblePlanResponse DeserializePlan(this PreparedAction action) =>
        JsonSerializer.Deserialize<ApplicationInstallationAnsiblePlanResponse>(action.PlanSnapshotJson)
            ?? throw new InvalidOperationException($"Prepared action {action.Id} has a corrupt plan snapshot.");

    public static ApplicationInstallationValidationResponse DeserializeValidation(this PreparedAction action) =>
        JsonSerializer.Deserialize<ApplicationInstallationValidationResponse>(action.ValidationSnapshotJson)
            ?? throw new InvalidOperationException($"Prepared action {action.Id} has a corrupt validation snapshot.");
}
