using Iris.Domain.Infrastructure;

namespace Iris.Application.Abstractions;

/// <summary>One SSH target to run a command against — host/port/username plus how to
/// authenticate. <see cref="Secret"/> is the resolved password or private key value (already
/// read out of <c>ISecretStore</c> by the caller), never a reference.</summary>
public sealed record RemoteCommandTarget(
    string Host,
    int Port,
    string Username,
    ServerCredentialAuthMethod AuthMethod,
    string Secret);

/// <summary>Same shape discipline as <c>Iris.Infrastructure.Processes.IProcessRunner</c>'s
/// <c>ProcessResult</c> — this is that same idea over SSH instead of a local process.</summary>
public sealed record RemoteCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut);

/// <summary>Runs one shell command on a remote host over SSH. The only place in this codebase
/// that opens a live SSH connection — everything else that models SSH (<c>ServerCredential</c>)
/// only stores a reference to how to connect, it never actually connects.</summary>
public interface IRemoteCommandRunner
{
    Task<RemoteCommandResult> RunAsync(
        RemoteCommandTarget target,
        string command,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
