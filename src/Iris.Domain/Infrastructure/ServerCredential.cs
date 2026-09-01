using Iris.Domain.Common;

namespace Iris.Domain.Infrastructure;

/// <summary>
/// One OS-login account on a <see cref="ServerNode"/> — either a <see cref="ServerCredentialKind.SystemUser"/>
/// (a named person, optionally tied to an Iris <c>User</c> via <see cref="OwnerUserId"/>) or a
/// <see cref="ServerCredentialKind.ServiceAccount"/> (a shared automation account such as <c>ansible</c>,
/// named by <see cref="ServiceName"/>). A server can hold several. <see cref="SecretReference"/> is a logical
/// pointer into the secret store (OpenBao in production); the actual password/key is never held here.
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
        ServerCredentialKind kind,
        Guid? ownerUserId,
        string? serviceName,
        string? label)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretReference);

        switch (kind)
        {
            case ServerCredentialKind.SystemUser:
                if (!string.IsNullOrWhiteSpace(serviceName))
                {
                    throw new ArgumentException("A system-user credential must not carry a service name.", nameof(serviceName));
                }

                if (ownerUserId == Guid.Empty)
                {
                    throw new ArgumentException("Owner user id must be a real id or null.", nameof(ownerUserId));
                }

                break;

            case ServerCredentialKind.ServiceAccount:
                if (string.IsNullOrWhiteSpace(serviceName))
                {
                    throw new ArgumentException("A service-account credential requires a service name.", nameof(serviceName));
                }

                if (ownerUserId is not null)
                {
                    throw new ArgumentException("A service-account credential cannot have an owner user.", nameof(ownerUserId));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown credential kind.");
        }

        ServerNodeId = serverNodeId;
        Username = username.Trim();
        AuthMethod = authMethod;
        SecretReference = secretReference;
        Kind = kind;
        OwnerUserId = ownerUserId;
        ServiceName = string.IsNullOrWhiteSpace(serviceName) ? null : serviceName.Trim();
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
    }

    public Guid ServerNodeId { get; private set; }

    public string Username { get; private set; }

    public ServerCredentialAuthMethod AuthMethod { get; private set; }

    public string SecretReference { get; private set; }

    public ServerCredentialKind Kind { get; private set; }

    /// <summary>The Iris <c>User</c> this OS login belongs to. Only meaningful for <see cref="ServerCredentialKind.SystemUser"/>.</summary>
    public Guid? OwnerUserId { get; private set; }

    /// <summary>Automation identity this account serves (e.g. <c>ansible</c>). Only for <see cref="ServerCredentialKind.ServiceAccount"/>.</summary>
    public string? ServiceName { get; private set; }

    public string? Label { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
