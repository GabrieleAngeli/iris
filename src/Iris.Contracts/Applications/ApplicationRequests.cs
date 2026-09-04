namespace Iris.Contracts.Applications;

/// <summary>Body of <c>POST /applications</c>. <c>Slug</c> is auto-generated from <c>Name</c> when omitted.</summary>
public sealed record CreateApplicationRequest(
    string Name,
    string? Slug,
    string RuntimeType,
    string RepositoryUrl,
    string DefaultBranch,
    string? Description,
    string? ArtifactProvider = null,
    string? ArtifactFeed = null,
    string? ArtifactName = null,
    string? ArtifactPath = null,
    string? BuildPipelineUrl = null);

/// <summary>Body of <c>PUT /applications/{applicationId}</c>. The catalog slug is immutable.</summary>
public sealed record UpdateApplicationRequest(
    string Name,
    string RuntimeType,
    string RepositoryUrl,
    string DefaultBranch,
    string? Description,
    bool IsActive,
    string? ArtifactProvider = null,
    string? ArtifactFeed = null,
    string? ArtifactName = null,
    string? ArtifactPath = null,
    string? BuildPipelineUrl = null);

public sealed record RuntimeMetadataRequest(
    string RuntimeName,
    string? PreferredOs,
    int? RequiredCpuCores,
    int? RequiredMemoryMb,
    IReadOnlyList<int>? RequiredPorts,
    IReadOnlyList<string>? ExecutionTargets = null,
    IReadOnlyList<RuntimeOsSupportInfo>? OsSupport = null,
    int? MinimumCpuCores = null,
    int? MinimumMemoryMb = null,
    IReadOnlyList<string>? PortKeys = null);

public sealed record RuntimeOsSupportInfo(
    string Type,
    string? Distribution,
    string? Version,
    bool Tested = true);

/// <summary>Body of <c>POST /applications/{applicationId}/versions</c>.</summary>
public sealed record AddApplicationVersionRequest(
    string Version,
    string? SourceReference,
    RuntimeMetadataRequest RuntimeMetadata);

public sealed record ConfigurationKeyInput(
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

public sealed record DependencyInput(
    string Name,
    string Category,
    bool Required,
    string? Description,
    string? PlaceholderKey,
    string? ProviderApplicationSlug = null,
    string? ProviderPlaceholderKey = null);

public sealed record PlaceholderInput(
    string Key,
    string? Category,
    string? Description,
    bool Required);

public sealed record ApplicationUnitInput(
    string Key,
    string? DisplayName,
    string? Kind,
    string? EntryPoint,
    string? ArtifactPath,
    IReadOnlyList<string>? ExecutionTargets = null,
    IReadOnlyList<string>? Profiles = null);

public sealed record InstallationProfileInput(
    string Key,
    string? DisplayName,
    bool Required,
    bool Multiple,
    IReadOnlyList<string>? ConfigurationKeys = null);

public sealed record DependencyConstraintInput(
    string? PlaceholderKey,
    string? ServiceKind,
    string? VersionExpression,
    string? DetailsJson = null);

/// <summary>
/// Body of <c>POST /applications/{applicationId}/versions/{versionId}/import</c> — the shape an
/// Iris Extractor package takes today (accepted directly via the API; a real pipeline-triggered
/// upload is a later increment).
/// </summary>
public sealed record ImportConfigurationPackageRequest(
    string SchemaVersion,
    IReadOnlyList<ConfigurationKeyInput> ConfigurationKeys,
    IReadOnlyList<DependencyInput> Dependencies,
    IReadOnlyList<PlaceholderInput> Placeholders,
    IReadOnlyList<string>? Warnings,
    IReadOnlyList<ApplicationUnitInput>? ApplicationUnits = null,
    IReadOnlyList<InstallationProfileInput>? InstallationProfiles = null,
    IReadOnlyList<DependencyConstraintInput>? DependencyConstraints = null);
