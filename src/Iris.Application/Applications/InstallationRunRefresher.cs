using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

/// <summary>
/// Polls AWX for one <see cref="InstallationRun"/>'s current status and applies it — shared by
/// <see cref="GetInstallationRunHandler"/> (refresh-on-read) and the background poller
/// (<c>PollActiveInstallationRunsHandler</c>), so "call AWX, map status, capture outcome once
/// terminal" exists in exactly one place.
/// </summary>
public interface IInstallationRunRefresher
{
    /// <summary>No-ops for a run that's already terminal, not an AWX job, or has no job id yet.
    /// Otherwise polls AWX, maps and applies the status, and — only on the poll where the run first
    /// becomes terminal — captures elapsed time and stdout. Never throws when AWX is unreachable or
    /// unconfigured (keeps the run's last known status); does not save — the caller owns the unit of work.</summary>
    Task RefreshAsync(InstallationRun run, CancellationToken cancellationToken = default);
}

public sealed class InstallationRunRefresher(IAwxClient awx, IClock clock) : IInstallationRunRefresher
{
    /// <summary>AWX job logs can run into the hundreds of KB for a busy playbook — keep the DB row
    /// bounded. The failure that matters is almost always at the end of the log, so the *last*
    /// this-many characters are kept, not the first.</summary>
    private const int MaxOutputLength = 50_000;

    public async Task RefreshAsync(InstallationRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (run.IsTerminal || run.Kind != InstallationRunKind.AwxJob || string.IsNullOrWhiteSpace(run.ExternalJobId))
        {
            return;
        }

        AwxJobStatusResult status;
        try
        {
            status = await awx.GetJobStatusAsync(run.ExternalJobId, cancellationToken).ConfigureAwait(false);
        }
        catch (ValidationException)
        {
            // AWX not configured / unreachable — keep the last known status, try again next time.
            return;
        }

        run.UpdateStatus(InstallationRunMapping.FromAwxStatus(status.Status), status.Message, clock.UtcNow);

        if (!run.IsTerminal)
        {
            return;
        }

        string? output = null;
        try
        {
            output = await awx.GetJobOutputAsync(run.ExternalJobId, cancellationToken).ConfigureAwait(false);
        }
        catch (ValidationException)
        {
            // Best-effort — the status/elapsed time already captured below still matters even if
            // the stdout fetch itself fails (e.g. transient AWX issue right as the job finishes).
        }

        run.CaptureOutcome(status.ElapsedSeconds, Truncate(output));
    }

    private static string? Truncate(string? output)
    {
        if (string.IsNullOrEmpty(output) || output.Length <= MaxOutputLength)
        {
            return output;
        }

        var marker = $"[log truncated — showing final {MaxOutputLength} characters]\n";
        return marker + output[^MaxOutputLength..];
    }
}
