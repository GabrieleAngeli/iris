using Iris.Domain.Common;
using Iris.Domain.Tenancy;

namespace Iris.Domain.Infrastructure;

/// <summary>
/// A registered server — shared or dedicated, self-hosted or cloud — that deployments can
/// target. Reachability (<see cref="PublicIpAddress"/>/<see cref="PrivateIpAddress"/>) and
/// the OS-login accounts (<see cref="Credentials"/>) tooling uses to reach it live here;
/// capability tags, resource sizing and port/endpoint inventory are a later increment.
/// </summary>
public sealed class ServerNode : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    private readonly List<ServerCredential> _credentials = [];

    // For the persistence layer.
    private ServerNode()
        : base(Guid.Empty)
    {
        Name = string.Empty;
    }

    public ServerNode(
        Guid id,
        string name,
        string? hostname,
        ServerOs os,
        ServerHostingType hostingType,
        string? publicIpAddress,
        string? privateIpAddress,
        ContextKind environment)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Hostname = string.IsNullOrWhiteSpace(hostname) ? null : hostname.Trim();
        Os = os;
        HostingType = hostingType;
        PublicIpAddress = string.IsNullOrWhiteSpace(publicIpAddress) ? null : publicIpAddress.Trim();
        PrivateIpAddress = string.IsNullOrWhiteSpace(privateIpAddress) ? null : privateIpAddress.Trim();
        Environment = environment;
        IsActive = true;
    }

    public string Name { get; private set; }

    public string? Hostname { get; private set; }

    public ServerOs Os { get; private set; }

    public ServerHostingType HostingType { get; private set; }

    public string? PublicIpAddress { get; private set; }

    public string? PrivateIpAddress { get; private set; }

    public ContextKind Environment { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<ServerCredential> Credentials => _credentials.AsReadOnly();

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ServerCredential AddCredential(
        Guid credentialId,
        string username,
        ServerCredentialAuthMethod authMethod,
        string secretReference,
        ServerCredentialKind kind,
        Guid? ownerUserId,
        string? serviceName,
        string? label)
    {
        if (_credentials.Any(c => string.Equals(c.Username, username.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Server '{Name}' already has a credential for user '{username}'.");
        }

        var credential = new ServerCredential(
            credentialId, Id, username, authMethod, secretReference, kind, ownerUserId, serviceName, label);
        _credentials.Add(credential);
        return credential;
    }

    public void RemoveCredential(Guid credentialId) =>
        _credentials.RemoveAll(c => c.Id == credentialId);

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
