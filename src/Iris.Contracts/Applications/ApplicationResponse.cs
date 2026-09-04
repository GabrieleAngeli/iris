namespace Iris.Contracts.Applications;

public sealed record RuntimeMetadataResponse(
    string RuntimeName,
    string? PreferredOs,
    int? RequiredCpuCores,
    int? RequiredMemoryMb,
    IReadOnlyList<int> RequiredPorts,
    IReadOnlyList<string> ExecutionTargets,
    IReadOnlyList<RuntimeOsSupportInfo> OsSupport,
    int? MinimumCpuCores,
    int? MinimumMemoryMb,
    IReadOnlyList<string> PortKeys);

public sealed record ApplicationVersionSummaryResponse(
    Guid Id,
    string Version,
    string? SourceReference,
    RuntimeMetadataResponse RuntimeMetadata,
    int ConfigurationKeyCount,
    int DependencyCount,
    int PlaceholderCount,
    int ApplicationUnitCount,
    int InstallationProfileCount,
    int DependencyConstraintCount,
    DateTimeOffset? LastImportedAtUtc);

public sealed record ApplicationResponse(
    Guid Id,
    string Name,
    string Slug,
    string RuntimeType,
    string RepositoryUrl,
    string DefaultBranch,
    string? Description,
    string? ArtifactProvider,
    string? ArtifactFeed,
    string? ArtifactName,
    string? ArtifactPath,
    string? BuildPipelineUrl,
    bool IsActive,
    IReadOnlyList<ApplicationVersionSummaryResponse> Versions);
