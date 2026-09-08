using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Settings;

namespace Iris.Application.Settings;

/// <summary>No parameters — the container name/image/port are fixed by design (a single,
/// well-known convenience instance, not a configurable deployment).</summary>
public sealed record ProvisionOpenBaoCommand;

/// <summary>
/// Starts a dev-mode OpenBao container on the same host Iris.Api runs on, via
/// <see cref="IContainerRuntime"/>, and persists the endpoint/root token it prints to its own
/// logs through <see cref="SaveOpenBaoIntegrationSettingsHandler"/> — the same persistence path
/// as "use an existing instance" in the setup wizard, so there is exactly one place that writes
/// <c>IntegrationSettings</c>' OpenBao fields.
///
/// Deliberately dev-mode/convenience-only: no persistent storage backend, no unseal/HA. Not
/// for a production deployment — an operator who needs that configures an existing hardened
/// OpenBao instance instead of calling this endpoint.
/// </summary>
public sealed class ProvisionOpenBaoHandler(
    IContainerRuntime containerRuntime,
    SaveOpenBaoIntegrationSettingsHandler saveOpenBao)
{
    internal const string ContainerName = "iris-openbao";
    private const string Image = "openbao/openbao:2.1";
    private const int Port = 8200;
    private const string RootTokenMarker = "Root Token: ";

    public async Task<ProvisionOpenBaoResponse> HandleAsync(
        ProvisionOpenBaoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await containerRuntime.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new ValidationException(
                "Docker is not available on this host (not installed, or its daemon isn't running). " +
                "Start Docker and try again, or configure an existing OpenBao instance instead.");
        }

        var status = await containerRuntime.GetStatusAsync(ContainerName, cancellationToken).ConfigureAwait(false);
        switch (status.State)
        {
            case ContainerState.Stopped:
                // Dev-mode OpenBao keeps no persistent state anyway (in-memory backend, fresh
                // unseal on every boot) — restarting the same container is exactly as safe as
                // creating a new one, and much friendlier than making the operator run `docker
                // rm` by hand first. Real bug found via manual testing (2026-09-08): this used
                // to throw here, which made "install for me" silently fail every time this
                // convenience container had been stopped since the last provision.
                await containerRuntime.StartAsync(ContainerName, cancellationToken).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                break;

            case ContainerState.Absent:
                var spec = new ContainerRunSpec(
                    ContainerName,
                    Image,
                    PortBindings: new Dictionary<int, int> { [Port] = Port },
                    Command: ["server", "-dev", $"-dev-listen-address=0.0.0.0:{Port}"],
                    ExtraArgs: ["--cap-add=IPC_LOCK"]);
                await containerRuntime.RunAsync(spec, cancellationToken).ConfigureAwait(false);
                // The dev server needs a moment to come up and print its banner before the
                // root token is readable from its logs.
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                break;

            case ContainerState.Running:
                // Already running from a prior call — fall through and read its logs as-is.
                break;
        }

        var logs = await containerRuntime.GetLogsAsync(ContainerName, cancellationToken).ConfigureAwait(false);
        var token = ParseRootToken(logs) ?? throw new ValidationException(
            $"OpenBao started, but its root token could not be read from the container's logs. " +
            $"Run 'docker logs {ContainerName}' to find it and configure OpenBao manually instead.");

        var endpoint = $"http://localhost:{Port}";
        await saveOpenBao
            .HandleAsync(new SaveOpenBaoIntegrationSettingsCommand(endpoint, token, "secret", UseKvV2: true), cancellationToken)
            .ConfigureAwait(false);

        return new ProvisionOpenBaoResponse(
            endpoint,
            RestartRequired: true,
            "OpenBao dev-mode container started — convenience/non-production use only. " +
            "Restart Iris.Api for this instance to start using it.");
    }

    /// <summary>OpenBao is a Vault fork and keeps Vault's dev-mode banner format: a line
    /// reading <c>Root Token: &lt;token&gt;</c>. Verified against a real
    /// <c>openbao/openbao:2.1</c> container run through this handler on a real Docker daemon
    /// (2026-09-08) — see <c>ParseRootToken_finds_the_token_in_a_real_captured_OpenBao_banner</c>
    /// in <c>ProvisionOpenBaoHandlerTests</c>, whose fixture is that container's actual log
    /// output, including the other "root token" mentions in the banner's prose that the marker
    /// match must not (and doesn't) false-positive on.
    ///
    /// Returns the LAST match, not the first: `docker logs` returns the container's entire
    /// history across every start, and a container that was stopped then restarted (see the
    /// <see cref="ContainerState.Stopped"/> branch above) generates a brand new banner — with
    /// a brand new token — appended after the old one. Taking the first match would silently
    /// keep handing out a stale, no-longer-valid token after every restart.</summary>
    internal static string? ParseRootToken(string logs)
    {
        string? found = null;
        foreach (var line in logs.Split('\n'))
        {
            var trimmed = line.Trim();
            var index = trimmed.IndexOf(RootTokenMarker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                found = trimmed[(index + RootTokenMarker.Length)..].Trim();
            }
        }

        return found;
    }
}
