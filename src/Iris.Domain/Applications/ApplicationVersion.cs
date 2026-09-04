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
    string? PlaceholderKey,
    string? ValueType = null,
    string? ItemType = null,
    string? Scope = null,
    string? SerializationJson = null,
    string? ResolutionJson = null,
    string? ProfilesJson = null,
    string? ProfileDefaultsJson = null,
    string? ItemSchemaJson = null);

/// <summary>Shape of one <see cref="DependencyDefinition"/> to create.</summary>
public sealed record NewDependencyDefinition(
    Guid Id,
    string Name,
    string Category,
    bool Required,
    string? Description,
    string? PlaceholderKey,
    string? ProviderApplicationSlug = null,
    string? ProviderPlaceholderKey = null);

/// <summary>Shape of one <see cref="PlaceholderDefinition"/> to create.</summary>
public sealed record NewPlaceholderDefinition(
    Guid Id,
    string Key,
    string? Category,
    string? Description,
    bool Required);

public sealed record NewApplicationUnitDefinition(
    Guid Id,
    string Key,
    string? DisplayName,
    string? Kind,
    string? EntryPoint,
    string? ArtifactPath,
    string? ExecutionTargetsJson,
    string? ProfilesJson);

public sealed record NewInstallationProfileDefinition(
    Guid Id,
    string Key,
    string? DisplayName,
    bool Required,
    bool Multiple,
    string? ConfigurationKeysJson);

public sealed record NewDependencyConstraintDefinition(
    Guid Id,
    string? PlaceholderKey,
    string? ServiceKind,
    string? VersionExpression,
    string? DetailsJson);

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
    private readonly List<ApplicationUnitDefinition> _applicationUnits = [];
    private readonly List<InstallationProfileDefinition> _installationProfiles = [];
    private readonly List<DependencyConstraintDefinition> _dependencyConstraints = [];

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

    public IReadOnlyCollection<ApplicationUnitDefinition> ApplicationUnits => _applicationUnits.AsReadOnly();

    public IReadOnlyCollection<InstallationProfileDefinition> InstallationProfiles => _installationProfiles.AsReadOnly();

    public IReadOnlyCollection<DependencyConstraintDefinition> DependencyConstraints => _dependencyConstraints.AsReadOnly();

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
        IReadOnlyList<NewApplicationUnitDefinition> applicationUnits,
        IReadOnlyList<NewInstallationProfileDefinition> installationProfiles,
        IReadOnlyList<NewDependencyConstraintDefinition> dependencyConstraints,
        IReadOnlyList<string> warnings,
        DateTimeOffset importedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPackageJson);

        _configurationKeys.Clear();
        _configurationKeys.AddRange(configurationKeys.Select(k => new ConfigurationKey(
            k.Id,
            Id,
            k.Key,
            k.TargetKind,
            k.Required,
            k.Secret,
            k.DefaultValue,
            k.Description,
            k.Purpose,
            k.PlaceholderKey,
            k.ValueType,
            k.ItemType,
            k.Scope,
            k.SerializationJson,
            k.ResolutionJson,
            k.ProfilesJson,
            k.ProfileDefaultsJson,
            k.ItemSchemaJson)));

        _dependencies.Clear();
        _dependencies.AddRange(dependencies.Select(d => new DependencyDefinition(
            d.Id, Id, d.Name, d.Category, d.Required, d.Description, d.PlaceholderKey, d.ProviderApplicationSlug, d.ProviderPlaceholderKey)));

        _placeholders.Clear();
        _placeholders.AddRange(placeholders.Select(p => new PlaceholderDefinition(
            p.Id, Id, p.Key, p.Category, p.Description, p.Required)));

        _applicationUnits.Clear();
        _applicationUnits.AddRange(applicationUnits.Select(u => new ApplicationUnitDefinition(
            u.Id, Id, u.Key, u.DisplayName, u.Kind, u.EntryPoint, u.ArtifactPath, u.ExecutionTargetsJson, u.ProfilesJson)));

        _installationProfiles.Clear();
        _installationProfiles.AddRange(installationProfiles.Select(p => new InstallationProfileDefinition(
            p.Id, Id, p.Key, p.DisplayName, p.Required, p.Multiple, p.ConfigurationKeysJson)));

        _dependencyConstraints.Clear();
        _dependencyConstraints.AddRange(dependencyConstraints.Select(c => new DependencyConstraintDefinition(
            c.Id, Id, c.PlaceholderKey, c.ServiceKind, c.VersionExpression, c.DetailsJson)));

        ImportWarnings = warnings.ToList();

        RawImportPackageJson = rawPackageJson;
        LastImportSchemaVersion = schemaVersion.Trim();
        LastImportedAtUtc = importedAtUtc;
    }
}
