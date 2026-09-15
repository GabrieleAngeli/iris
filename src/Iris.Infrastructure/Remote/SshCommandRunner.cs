using System.Text;
using Iris.Application.Abstractions;
using Iris.Domain.Infrastructure;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Iris.Infrastructure.Remote;

/// <summary>
/// The only place in this codebase that opens a live SSH connection. Used today to run the AWX
/// automation repo's blueprint-sync playbook on the ops host exactly as an operator would by
/// hand (see <c>SyncAwxBlueprintHandler</c>) — never for anything against a customer server.
/// </summary>
internal sealed class SshCommandRunner : IRemoteCommandRunner
{
    public async Task<RemoteCommandResult> RunAsync(
        RemoteCommandTarget target,
        string command,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        using var client = new SshClient(BuildConnectionInfo(target, timeout));

        try
        {
            await Task.Run(client.Connect, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SshException or System.Net.Sockets.SocketException or TimeoutException)
        {
            return new RemoteCommandResult(-1, string.Empty, $"Could not connect to {target.Host}:{target.Port} — {ex.Message}", TimedOut: false);
        }

        try
        {
            using var sshCommand = client.CreateCommand(command);

            var executeTask = Task.Run(() => sshCommand.Execute(), cancellationToken);
            var winner = await Task.WhenAny(executeTask, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);

            if (winner != executeTask)
            {
                return new RemoteCommandResult(-1, string.Empty, string.Empty, TimedOut: true);
            }

            var standardOutput = await executeTask.ConfigureAwait(false);
            return new RemoteCommandResult(sshCommand.ExitStatus ?? -1, standardOutput, sshCommand.Error, TimedOut: false);
        }
        finally
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
    }

    private static ConnectionInfo BuildConnectionInfo(RemoteCommandTarget target, TimeSpan timeout)
    {
        AuthenticationMethod authenticationMethod = target.AuthMethod switch
        {
            ServerCredentialAuthMethod.Password => new PasswordAuthenticationMethod(target.Username, target.Secret),
            ServerCredentialAuthMethod.SshKey => new PrivateKeyAuthenticationMethod(target.Username, BuildPrivateKeyFile(target.Secret)),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target.AuthMethod, "Unknown auth method."),
        };

        return new ConnectionInfo(target.Host, target.Port, target.Username, authenticationMethod)
        {
            Timeout = timeout,
        };
    }

    private static PrivateKeyFile BuildPrivateKeyFile(string privateKeyText)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(privateKeyText));
        return new PrivateKeyFile(stream);
    }
}
