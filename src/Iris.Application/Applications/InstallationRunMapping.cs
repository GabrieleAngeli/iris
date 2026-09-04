using System.Text.Json;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

internal static class InstallationRunMapping
{
    public static InstallationRunResponse ToResponse(this InstallationRun run) => new(
        run.Id,
        run.ApplicationInstallationId,
        run.Kind.ToString(),
        run.Status.ToString(),
        run.IsTerminal,
        run.ExternalJobId,
        run.ExternalUrl,
        run.Message,
        PreviewVariables(run.SubmittedVariablesJson),
        run.CreatedAtUtc,
        run.UpdatedAtUtc,
        run.CompletedAtUtc);

    /// <summary>Maps an AWX job status string onto the Iris run lifecycle.</summary>
    public static InstallationRunStatus FromAwxStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "successful" => InstallationRunStatus.Succeeded,
        "failed" or "error" => InstallationRunStatus.Failed,
        "canceled" or "cancelled" => InstallationRunStatus.Canceled,
        "running" => InstallationRunStatus.Running,
        _ => InstallationRunStatus.Pending,
    };

    private static IReadOnlyDictionary<string, string?> PreviewVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>();
        }
    }
}
