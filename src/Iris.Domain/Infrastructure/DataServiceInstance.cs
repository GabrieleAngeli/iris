using Iris.Domain.Common;
using Iris.Domain.Tenancy;

namespace Iris.Domain.Infrastructure;

/// <summary>Managed database/cache endpoint such as RDS SQL Server, RDS PostgreSQL or Redis.</summary>
public sealed class DataServiceInstance : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    private DataServiceInstance()
        : base(Guid.Empty)
    {
        Name = string.Empty;
        Endpoint = string.Empty;
    }

    public DataServiceInstance(
        Guid id,
        string name,
        DataServiceKind kind,
        string endpoint,
        int? port,
        string? username,
        string passwordSecretReference,
        string? version,
        string? size,
        int? storageGb,
        ContextKind environment)
        : base(id)
    {
        Name = string.Empty;
        Endpoint = string.Empty;
        Apply(name, kind, endpoint, port, username, passwordSecretReference, version, size, storageGb, environment, isActive: true);
    }

    public string Name { get; private set; }

    public DataServiceKind Kind { get; private set; }

    public string Endpoint { get; private set; }

    public int? Port { get; private set; }

    public string? Username { get; private set; }

    public string? PasswordSecretReference { get; private set; }

    public string? Version { get; private set; }

    public string? Size { get; private set; }

    public int? StorageGb { get; private set; }

    public ContextKind Environment { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public void Update(
        string name,
        DataServiceKind kind,
        string endpoint,
        int? port,
        string? username,
        string? passwordSecretReference,
        string? version,
        string? size,
        int? storageGb,
        ContextKind environment,
        bool isActive) =>
        Apply(name, kind, endpoint, port, username, passwordSecretReference, version, size, storageGb, environment, isActive);

    public void ApplyInventoryDiscovery(DataServiceKind kind, string? version, string? size, int? storageGb)
    {
        Kind = kind;
        Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        Size = string.IsNullOrWhiteSpace(size) ? null : size.Trim();
        StorageGb = storageGb;
    }

    private void Apply(
        string name,
        DataServiceKind kind,
        string endpoint,
        int? port,
        string? username,
        string? passwordSecretReference,
        string? version,
        string? size,
        int? storageGb,
        ContextKind environment,
        bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        }

        if (storageGb < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(storageGb), "Storage cannot be negative.");
        }

        Name = name.Trim();
        Kind = kind;
        Endpoint = endpoint.Trim();
        Port = port;
        Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
        PasswordSecretReference = string.IsNullOrWhiteSpace(passwordSecretReference) ? null : passwordSecretReference.Trim();
        Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        Size = string.IsNullOrWhiteSpace(size) ? null : size.Trim();
        StorageGb = storageGb;
        Environment = environment;
        IsActive = isActive;
    }
}
