namespace Iris.Contracts.Applications;

/// <summary>Body of <c>POST /applications</c>. <c>Slug</c> is auto-generated from <c>Name</c> when omitted.</summary>
public sealed record CreateApplicationRequest(
    string Name,
    string? Slug,
    string RuntimeType,
    string RepositoryUrl,
    string DefaultBranch,
    string? Description);

public sealed record RuntimeMetadataRequest(
    string RuntimeName,
    string? PreferredOs,
    int? RequiredCpuCores,
    int? RequiredMemoryMb,
    IReadOnlyList<int>? RequiredPorts);

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
    string? PlaceholderKey);

public sealed record DependencyInput(
    string Name,
    string Category,
    bool Required,
    string? Description,
    string? PlaceholderKey);

public sealed record PlaceholderInput(
    string Key,
    string? Category,
    string? Description,
    bool Required);

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
    IReadOnlyList<string>? Warnings);
