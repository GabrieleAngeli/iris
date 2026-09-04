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
    string? PlaceholderKey,
    string? ValueType,
    string? ItemType,
    string? Scope,
    string? SerializationJson,
    string? ResolutionJson,
    string? ProfilesJson,
    string? ProfileDefaultsJson,
    string? ItemSchemaJson);

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

public sealed record ApplicationUnitResponse(
    Guid Id,
    string Key,
    string? DisplayName,
    string? Kind,
    string? EntryPoint,
    string? ArtifactPath,
    IReadOnlyList<string> ExecutionTargets,
    IReadOnlyList<string> Profiles);

public sealed record InstallationProfileResponse(
    Guid Id,
    string Key,
    string? DisplayName,
    bool Required,
    bool Multiple,
    IReadOnlyList<string> ConfigurationKeys);

public sealed record DependencyConstraintResponse(
    Guid Id,
    string? PlaceholderKey,
    string? ServiceKind,
    string? VersionExpression,
    string? DetailsJson);

public sealed record ApplicationVersionDetailResponse(
    Guid Id,
    Guid ApplicationId,
    string Version,
    string? SourceReference,
    RuntimeMetadataResponse RuntimeMetadata,
    IReadOnlyList<ConfigurationKeyResponse> ConfigurationKeys,
    IReadOnlyList<DependencyResponse> Dependencies,
    IReadOnlyList<PlaceholderResponse> Placeholders,
    IReadOnlyList<ApplicationUnitResponse> ApplicationUnits,
    IReadOnlyList<InstallationProfileResponse> InstallationProfiles,
    IReadOnlyList<DependencyConstraintResponse> DependencyConstraints,
    IReadOnlyList<string> ImportWarnings,
    DateTimeOffset? LastImportedAtUtc,
    string? LastImportSchemaVersion);
