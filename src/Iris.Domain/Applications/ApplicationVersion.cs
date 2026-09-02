using Iris.Domain.Common;

namespace Iris.Domain.Applications;

/// <summary>Shape of one <see cref="ConfigurationKey"/> to create — id minted by the caller (handler), per convention.</summary>
public sealed record NewConfigurationKey(
    Guid Id,
    string Key,
    string TargetKind,
    bool Required,
    bool Secret,
    string? DefaultValue,
    string? Description,
    string? Purpose,
    string? PlaceholderKey);

/// <summary>Shape of one <see cref="DependencyDefinition"/> to create.</summary>
public sealed record NewDependencyDefinition(
    Guid Id,
    string Name,
    string Category,
    bool Required,
    string? Description,
    string? PlaceholderKey);

/// <summary>Shape of one <see cref="PlaceholderDefinition"/> to create.</summary>
public sealed record NewPlaceholderDefinition(
    Guid Id,
    string Key,
    string? Category,
    string? Description,
    bool Required);

/// <summary>
/// One released version of an <see cref="ApplicationDefinition"/>. Carries what the Iris Extractor
/// found the last time its configuration knowledge was imported (<see cref="ApplyImport"/>) — the
/// current snapshot, not a history of every import (the raw package is kept for audit/reprocessing,
/// see <see cref="RawImportPackageJson"/>, but superseded imports themselves are not retained).
/// </summary>
public sealed class ApplicationVersion : Entity<Guid>, IAuditableEntity
{
    private readonly List<ConfigurationKey> _configurationKeys = [];
    private readonly List<DependencyDefinition> _dependencies = [];
    private readonly List<PlaceholderDefinition> _placeholders = [];

    // For the persistence layer.
    private ApplicationVersion()
        : base(Guid.Empty)
    {
        Version = string.Empty;
        RuntimeMetadata = null!;
        ImportWarnings = [];
    }

    internal ApplicationVersion(Guid id, Guid applicationId, string version, string? sourceReference, RuntimeMetadata runtimeMetadata)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(runtimeMetadata);

        ApplicationId = applicationId;
        Version = version.Trim();
        SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference.Trim();
        RuntimeMetadata = runtimeMetadata;
        ImportWarnings = [];
    }

    public Guid ApplicationId { get; private set; }

    public string Version { get; private set; }

    public string? SourceReference { get; private set; }

    public RuntimeMetadata RuntimeMetadata { get; private set; }

    public IReadOnlyCollection<ConfigurationKey> ConfigurationKeys => _configurationKeys.AsReadOnly();

    public IReadOnlyCollection<DependencyDefinition> Dependencies => _dependencies.AsReadOnly();

    public IReadOnlyCollection<PlaceholderDefinition> Placeholders => _placeholders.AsReadOnly();

    public IReadOnlyList<string> ImportWarnings { get; private set; }

    /// <summary>The original extracted package, kept verbatim for audit and later reprocessing.</summary>
    public string? RawImportPackageJson { get; private set; }

    public string? LastImportSchemaVersion { get; private set; }

    public DateTimeOffset? LastImportedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Applies a freshly extracted configuration package: replaces the current configuration
    /// keys/dependencies/placeholders/warnings wholesale (this is the current snapshot, not an
    /// accumulating history) and keeps the raw package for audit.
    /// </summary>
    public void ApplyImport(
        string schemaVersion,
        string rawPackageJson,
        IReadOnlyList<NewConfigurationKey> configurationKeys,
        IReadOnlyList<NewDependencyDefinition> dependencies,
        IReadOnlyList<NewPlaceholderDefinition> placeholders,
        IReadOnlyList<string> warnings,
        DateTimeOffset importedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPackageJson);

        _configurationKeys.Clear();
        _configurationKeys.AddRange(configurationKeys.Select(k => new ConfigurationKey(
            k.Id, Id, k.Key, k.TargetKind, k.Required, k.Secret, k.DefaultValue, k.Description, k.Purpose, k.PlaceholderKey)));

        _dependencies.Clear();
        _dependencies.AddRange(dependencies.Select(d => new DependencyDefinition(
            d.Id, Id, d.Name, d.Category, d.Required, d.Description, d.PlaceholderKey)));

        _placeholders.Clear();
        _placeholders.AddRange(placeholders.Select(p => new PlaceholderDefinition(
            p.Id, Id, p.Key, p.Category, p.Description, p.Required)));

        ImportWarnings = warnings.ToList();

        RawImportPackageJson = rawPackageJson;
        LastImportSchemaVersion = schemaVersion.Trim();
        LastImportedAtUtc = importedAtUtc;
    }
}
