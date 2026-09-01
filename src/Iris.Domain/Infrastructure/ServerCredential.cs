using Iris.Domain.Common;

namespace Iris.Domain.Infrastructure;

/// <summary>
/// One OS-login account on a <see cref="ServerNode"/> (e.g. <c>root</c>, or a <c>deploy</c>
/// service account) — a server can hold several, since different tooling or operators may
/// need their own credential. <see cref="SecretReference"/> is a logical pointer into the
/// secret store (OpenBao in production); the actual password/key is never held here.
/// </summary>
public sealed class ServerCredential : Entity<Guid>, IAuditableEntity
{
    // For the persistence layer.
    private ServerCredential()
        : base(Guid.Empty)
    {
        Username = string.Empty;
        SecretReference = string.Empty;
    }

    internal ServerCredential(
        Guid id,
        Guid serverNodeId,
        string username,
        ServerCredentialAuthMethod authMethod,
        string secretReference,
        string? label)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretReference);

        ServerNodeId = serverNodeId;
        Username = username.Trim();
        AuthMethod = authMethod;
        SecretReference = secretReference;
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
    }

    public Guid ServerNodeId { get; private set; }

    public string Username { get; private set; }

    public ServerCredentialAuthMethod AuthMethod { get; private set; }

    public string SecretReference { get; private set; }

    public string? Label { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
