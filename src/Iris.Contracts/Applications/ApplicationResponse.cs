namespace Iris.Contracts.Applications;

public sealed record RuntimeMetadataResponse(
    string RuntimeName,
    string? PreferredOs,
    int? RequiredCpuCores,
    int? RequiredMemoryMb,
    IReadOnlyList<int> RequiredPorts);

public sealed record ApplicationVersionSummaryResponse(
    Guid Id,
    string Version,
    string? SourceReference,
    RuntimeMetadataResponse RuntimeMetadata,
    int ConfigurationKeyCount,
    int DependencyCount,
    int PlaceholderCount,
    DateTimeOffset? LastImportedAtUtc);

public sealed record ApplicationResponse(
    Guid Id,
    string Name,
    string Slug,
    string RuntimeType,
    string RepositoryUrl,
    string DefaultBranch,
    string? Description,
    bool IsActive,
    IReadOnlyList<ApplicationVersionSummaryResponse> Versions);
