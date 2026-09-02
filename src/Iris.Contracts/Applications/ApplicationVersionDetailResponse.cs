namespace Iris.Contracts.Applications;

public sealed record ConfigurationKeyResponse(
    Guid Id,
    string Key,
    string TargetKind,
    bool Required,
    bool Secret,
    string? DefaultValue,
    string? Description,
    string? Purpose,
    string? PlaceholderKey);

public sealed record DependencyResponse(
    Guid Id,
    string Name,
    string Category,
    bool Required,
    string? Description,
    string? PlaceholderKey,
    string? ProviderApplicationSlug,
    string? ProviderPlaceholderKey);

public sealed record PlaceholderResponse(
    Guid Id,
    string Key,
    string? Category,
    string? Description,
    bool Required);

public sealed record ApplicationVersionDetailResponse(
    Guid Id,
    Guid ApplicationId,
    string Version,
    string? SourceReference,
    RuntimeMetadataResponse RuntimeMetadata,
    IReadOnlyList<ConfigurationKeyResponse> ConfigurationKeys,
    IReadOnlyList<DependencyResponse> Dependencies,
    IReadOnlyList<PlaceholderResponse> Placeholders,
    IReadOnlyList<string> ImportWarnings,
    DateTimeOffset? LastImportedAtUtc,
    string? LastImportSchemaVersion);
