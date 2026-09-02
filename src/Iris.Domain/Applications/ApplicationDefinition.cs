using Iris.Domain.Common;

namespace Iris.Domain.Applications;

/// <summary>
/// A catalogued application/service, imported into Iris via source repository + build pipeline
/// rather than a hand-maintained manifest. Owns its <see cref="Versions"/>; each version carries
/// the configuration knowledge extracted for it (see <see cref="ApplicationVersion"/>).
/// </summary>
public sealed class ApplicationDefinition : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    private readonly List<ApplicationVersion> _versions = [];

    // For the persistence layer.
    private ApplicationDefinition()
        : base(Guid.Empty)
    {
        Name = string.Empty;
        Slug = string.Empty;
        RepositoryUrl = string.Empty;
        DefaultBranch = string.Empty;
    }

    public ApplicationDefinition(
        Guid id,
        string name,
        string slug,
        ApplicationRuntimeType runtimeType,
        string repositoryUrl,
        string defaultBranch,
        string? description)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultBranch);

        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        RuntimeType = runtimeType;
        RepositoryUrl = repositoryUrl.Trim();
        DefaultBranch = defaultBranch.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = true;
    }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public ApplicationRuntimeType RuntimeType { get; private set; }

    public string RepositoryUrl { get; private set; }

    public string DefaultBranch { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<ApplicationVersion> Versions => _versions.AsReadOnly();

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ApplicationVersion AddVersion(Guid versionId, string version, string? sourceReference, RuntimeMetadata runtimeMetadata)
    {
        if (_versions.Any(v => string.Equals(v.Version, version.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Application '{Name}' already has a version '{version}'.");
        }

        var applicationVersion = new ApplicationVersion(versionId, Id, version, sourceReference, runtimeMetadata);
        _versions.Add(applicationVersion);
        return applicationVersion;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
