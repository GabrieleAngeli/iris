using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

/// <summary>
/// Command for <c>POST /system/integrations/awx/sync-blueprint</c>. Runs, over SSH on the ops
/// host, the exact command an operator already runs by hand today
/// (<c>ansible-playbook playbooks/ops/ops_awx_sync_blueprint.yml</c> from the AWX automation
/// repo's checkout) — reconciling AWX's real Job Templates/Credentials against what the repo's
/// blueprint manifest declares. Always human-triggered (from the "Sync now" button shown when
/// <c>AwxBlueprintDriftConnector</c> reports drift) — Iris never runs this unattended.
/// </summary>
public sealed record SyncAwxBlueprintCommand;

public sealed class SyncAwxBlueprintHandler(
    IIntegrationSettingsRepository settingsRepository,
    ISecretStore secretStore,
    IRemoteCommandRunner remoteCommandRunner)
{
    /// <summary>Comfortably under the MAUI client's default HttpClient timeout (100s, unset in
    /// MauiProgram.cs) — same margin already chosen for <c>AwxServerInventoryProbe</c>'s AWX
    /// polling, for the same reason: report back as a clear, persisted-in-the-response timeout
    /// rather than the client-side request aborting first with no explanation.</summary>
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(80);

    public async Task<SyncAwxBlueprintResponse> HandleAsync(
        SyncAwxBlueprintCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings is null || string.IsNullOrWhiteSpace(settings.OpsHostEndpoint))
        {
            throw new ValidationException("Configure the ops host (SSH endpoint/credentials) before syncing the AWX blueprint.");
        }

        if (string.IsNullOrWhiteSpace(settings.OpsHostUsername))
        {
            throw new ValidationException("The ops host has no SSH username configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.OpsHostSecretReference))
        {
            throw new ValidationException("No ops host SSH credential is stored — set it in the Configure ops host dialog first.");
        }

        var secret = await secretStore.RetrieveAsync(settings.OpsHostSecretReference, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ValidationException(
                "The ops host SSH credential isn't available yet — unlock the fallback secrets first (System settings), or set it in configuration.");
        }

        var target = new RemoteCommandTarget(
            settings.OpsHostEndpoint!, settings.OpsHostPort, settings.OpsHostUsername!, settings.OpsHostAuthMethod, secret);

        // Single quotes around the repo path: it's operator-configured, not user input from an
        // untrusted request, but quoting it defends against spaces/shell metacharacters either way.
        var playbookCommand = $"cd '{settings.OpsAwxRepoPath}' && ansible-playbook playbooks/ops/ops_awx_sync_blueprint.yml";

        var result = await remoteCommandRunner
            .RunAsync(target, playbookCommand, SyncTimeout, cancellationToken)
            .ConfigureAwait(false);

        if (result.TimedOut)
        {
            throw new ValidationException(
                $"Timed out after {SyncTimeout.TotalSeconds:0}s waiting for the AWX blueprint sync to finish on the ops host.");
        }

        if (result.ExitCode != 0)
        {
            var tail = Tail(result.StandardError.Length > 0 ? result.StandardError : result.StandardOutput);
            throw new ValidationException($"AWX blueprint sync failed (exit code {result.ExitCode}): {tail}");
        }

        return new SyncAwxBlueprintResponse(true, Tail(result.StandardOutput), null);
    }

    /// <summary>The last part of a potentially long ansible-playbook run — long enough to show
    /// the outcome (recap/failure line), short enough not to blow up the response body.</summary>
    private static string Tail(string text, int maxLength = 4000) =>
        text.Length <= maxLength ? text : text[^maxLength..];
}
