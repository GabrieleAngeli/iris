namespace Iris.Application.Abstractions;

/// <summary>
/// Where real secret values (passwords, SSH private keys, API keys, certificates…) live.
/// Iris's own database never holds one — only the logical reference this returns. In
/// production this is backed by OpenBao; see <c>Iris.Infrastructure/Secrets</c> for the
/// current mock adapter.
/// </summary>
public interface ISecretStore
{
    /// <summary>Stores <paramref name="secretValue"/> and returns the reference to persist in its place.</summary>
    Task<string> StoreAsync(string logicalPath, string secretValue, CancellationToken cancellationToken = default);

    /// <summary>Removes the secret behind a reference previously returned by <see cref="StoreAsync"/>.</summary>
    Task DeleteAsync(string reference, CancellationToken cancellationToken = default);
}
