using Iris.Application.Abstractions;
using Iris.Infrastructure.Remote;

namespace Iris.Infrastructure.Integrations;

/// <summary>Reachability for the ops host Iris SSHes into for <c>SyncAwxBlueprintHandler</c>. A
/// probe opens a real SSH connection and runs a trivial command — not the actual sync playbook.</summary>
internal sealed class OpsHostConnector(OpsHostOptions options, ISecretStore secretStore, IRemoteCommandRunner runner)
    : IIntegrationConnector
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    public string Key => "ops-host";

    public string Name => "Ops host";

    public string? Endpoint => options.Endpoint;

    public async Task<IntegrationConnectorStatus> GetStatusAsync(
        bool probe = false,
        CancellationToken cancellationToken = default)
    {
        if (!options.IsConfigured)
        {
            return new IntegrationConnectorStatus(Key, Name, "Not configured", Endpoint, "Endpoint, username and an SSH credential are required.");
        }

        if (!probe)
        {
            return new IntegrationConnectorStatus(Key, Name, "Configured", Endpoint, $"Repo path: {options.RepoPath}");
        }

        try
        {
            var secret = !string.IsNullOrWhiteSpace(options.Secret)
                ? options.Secret
                : await secretStore.RetrieveAsync(options.SecretReference!, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(secret))
            {
                return new IntegrationConnectorStatus(
                    Key, Name, "Unreachable", Endpoint,
                    "SSH credential isn't available yet — unlock the fallback secrets in System settings, or set it in configuration.");
            }

            var target = new RemoteCommandTarget(options.Endpoint!, options.Port, options.Username!, options.AuthMethod, secret);
            var result = await runner.RunAsync(target, "echo ok", ProbeTimeout, cancellationToken).ConfigureAwait(false);

            if (result.TimedOut)
            {
                return new IntegrationConnectorStatus(Key, Name, "Unreachable", Endpoint, $"Timed out after {ProbeTimeout.TotalSeconds:0}s.");
            }

            return result.ExitCode == 0
                ? new IntegrationConnectorStatus(Key, Name, "Reachable", Endpoint, null)
                : new IntegrationConnectorStatus(Key, Name, "Unreachable", Endpoint, result.StandardError);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Broad on purpose: SSH.NET surfaces connection/auth/channel failures through several
            // different exception types, and this probe must never itself throw — it exists to
            // report "unreachable" clearly, not to propagate a 500.
            return new IntegrationConnectorStatus(Key, Name, "Unreachable", Endpoint, ex.Message);
        }
    }
}
