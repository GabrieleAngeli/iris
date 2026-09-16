using Iris.Domain.Infrastructure;

namespace Iris.Infrastructure.Integrations;

/// <summary>Where Iris SSHes to run the AWX automation repo's blueprint-sync playbook — see
/// <c>SyncAwxBlueprintHandler</c>. Not the same concept as <c>ServerNode</c>/<c>ServerCredential</c>:
/// the ops host is a singular, special-purpose target, not a customer server Iris deploys to.</summary>
internal sealed class OpsHostOptions
{
    public string? Endpoint { get; set; }

    public int Port { get; set; } = 22;

    public string? Username { get; set; }

    public ServerCredentialAuthMethod AuthMethod { get; set; } = ServerCredentialAuthMethod.SshKey;

    /// <summary>Password or private key value provided directly by config/env.</summary>
    public string? Secret { get; set; }

    public string? SecretReference { get; set; }

    public string RepoPath { get; set; } = "/home/ops/Refactoring_ops_flow/awx";

    public bool HasSecret => !string.IsNullOrWhiteSpace(Secret) || !string.IsNullOrWhiteSpace(SecretReference);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Username) && HasSecret;
}
