using Iris.Domain.Common;
using Iris.Domain.Tenancy;

namespace Iris.Domain.Infrastructure;

/// <summary>
/// A registered server — shared or dedicated, self-hosted or cloud — that deployments can
/// target. Reachability (<see cref="PublicIpAddress"/>/<see cref="PrivateIpAddress"/>) and
/// the OS-login accounts (<see cref="Credentials"/>) tooling uses to reach it live here,
/// along with what it can host (<see cref="Capabilities"/>), what capacity it has
/// (<see cref="Resources"/>) and what ports are already spoken for (<see cref="UsedPorts"/>)
/// — the counterpart the future Validation Engine will compare against an
/// <c>ApplicationVersion.RuntimeMetadata</c>.
/// </summary>
public sealed class ServerNode : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    private readonly List<ServerCredential> _credentials = [];
    private readonly List<ServerDisk> _disks = [];

    // For the persistence layer.
    private ServerNode()
        : base(Guid.Empty)
    {
        Name = string.Empty;
        Capabilities = [];
        UsedPorts = [];
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
        Capabilities = [];
        UsedPorts = [];
    }

    public string Name { get; private set; }

    public string? Hostname { get; private set; }

    public ServerOs Os { get; private set; }

    public string? OsVersion { get; private set; }

    public string? MachineSize { get; private set; }

    public ServerHostingType HostingType { get; private set; }

    public string? PublicIpAddress { get; private set; }

    public string? PrivateIpAddress { get; private set; }

    public ContextKind Environment { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>What this server can host — a plain scalar collection, not a navigation.</summary>
    public IReadOnlyList<NodeCapability> Capabilities { get; private set; }

    /// <summary>Resource hints, as far as the operator knows them. Absent until set.</summary>
    public ResourceProfile? Resources { get; private set; }

    /// <summary>Ports already spoken for on this server — a plain scalar collection.</summary>
    public IReadOnlyList<int> UsedPorts { get; private set; }

    public IReadOnlyCollection<ServerCredential> Credentials => _credentials.AsReadOnly();

    /// <summary>Filesystem mounts found on the last successful discovery. Empty until then.</summary>
    public IReadOnlyCollection<ServerDisk> Disks => _disks.AsReadOnly();

    /// <summary>Null until the first discovery attempt; then whether the server answered.</summary>
    public bool? IsReachable { get; private set; }

    public DateTimeOffset? LastDiscoveredAtUtc { get; private set; }

    /// <summary>Why the last discovery attempt failed. Null when it succeeded (or never ran).</summary>
    public string? LastDiscoveryError { get; private set; }

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

    /// <summary>Replaces the server's identity and network details (credentials are untouched).</summary>
    public void UpdateDetails(
        string name,
        string? hostname,
        ServerOs os,
        ServerHostingType hostingType,
        string? publicIpAddress,
        string? privateIpAddress,
        ContextKind environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Hostname = string.IsNullOrWhiteSpace(hostname) ? null : hostname.Trim();
        Os = os;
        OsVersion = null;
        MachineSize = null;
        HostingType = hostingType;
        PublicIpAddress = string.IsNullOrWhiteSpace(publicIpAddress) ? null : publicIpAddress.Trim();
        PrivateIpAddress = string.IsNullOrWhiteSpace(privateIpAddress) ? null : privateIpAddress.Trim();
        Environment = environment;
    }

    /// <summary>
    /// Applies the outcome of a discovery probe (<c>IServerInventoryProbe</c>). Reachability,
    /// the timestamp and the error (if any) are always recorded. When the server was
    /// unreachable, nothing else changes — the last known-good OS/capacity/disk data is kept
    /// rather than blanked by a transient failure. <paramref name="usedPorts"/> is untouched by
    /// discovery itself (the probe passes the server's own current value straight through — no
    /// port scanning happens here or in any probe).
    /// </summary>
    public void ApplyInventoryDiscovery(
        bool isReachable,
        DateTimeOffset discoveredAtUtc,
        string? error,
        ServerOs os,
        string? osVersion,
        string? machineSize,
        IEnumerable<NodeCapability> capabilities,
        ResourceProfile? resources,
        IEnumerable<int> usedPorts,
        IEnumerable<ServerDiskInput> disks)
    {
        IsReachable = isReachable;
        LastDiscoveredAtUtc = discoveredAtUtc;
        LastDiscoveryError = isReachable ? null : error;

        if (!isReachable)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(usedPorts);
        ArgumentNullException.ThrowIfNull(disks);

        Os = os;
        OsVersion = string.IsNullOrWhiteSpace(osVersion) ? null : osVersion.Trim();
        MachineSize = string.IsNullOrWhiteSpace(machineSize) ? null : machineSize.Trim();
        UpdateCapacity(capabilities, resources, usedPorts, disks);
    }

    /// <summary>
    /// Replaces what this server can host, its resource hints and its known used ports —
    /// wholesale (the current picture, not an incremental one), kept separate from
    /// <see cref="UpdateDetails"/> since it changes on its own cadence. <paramref name="disks"/>
    /// is only replaced when given (discovery always supplies it; the manual capacity-entry
    /// path in <c>UpdateServerCapacity</c> doesn't know about disks and leaves them alone).
    /// </summary>
    public void UpdateCapacity(
        IEnumerable<NodeCapability> capabilities,
        ResourceProfile? resources,
        IEnumerable<int> usedPorts,
        IEnumerable<ServerDiskInput>? disks = null)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(usedPorts);

        Capabilities = capabilities.Distinct().ToList();
        Resources = resources;
        UsedPorts = usedPorts.Distinct().Order().ToList();

        if (disks is null)
        {
            return;
        }

        _disks.Clear();
        foreach (var disk in disks)
        {
            _disks.Add(new ServerDisk(
                Guid.CreateVersion7(), Id, disk.DeviceName, disk.MountPoint, disk.FileSystem, disk.TotalGb, disk.FreeGb));
        }
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
