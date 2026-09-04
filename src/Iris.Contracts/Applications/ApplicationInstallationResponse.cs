namespace Iris.Contracts.Applications;

public sealed record ApplicationInstallationBindingResponse(
    Guid Id,
    string PlaceholderKey,
    string TargetKind,
    Guid? TargetId,
    string? TargetSlug,
    string? ValuePreview,
    string? Notes);

public sealed record ApplicationInstallationResponse(
    Guid Id,
    string Name,
    Guid ApplicationId,
    string ApplicationName,
    string ApplicationSlug,
    Guid ApplicationVersionId,
    string Version,
    string? ApplicationUnitKey,
    string? InstallationProfileKey,
    Guid ServerNodeId,
    string ServerName,
    string Environment,
    string? Notes,
    bool IsActive,
    IReadOnlyList<ApplicationInstallationBindingResponse> Bindings,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ApplicationInstallationAnsibleVariableResponse(
    string Name,
    string ConfigurationKey,
    string? PlaceholderKey,
    string TargetTemplate,
    string ValueType,
    bool Required,
    bool Secret,
    string Source,
    string? ValuePreview,
    string? Notes);

public sealed record ApplicationInstallationAnsiblePlanResponse(
    Guid InstallationId,
    string InstallationName,
    string ApplicationSlug,
    string ApplicationVersion,
    string? ApplicationUnitKey,
    string? InstallationProfileKey,
    string Environment,
    string ServerName,
    IReadOnlyList<string> TemplateTargets,
    IReadOnlyList<ApplicationInstallationAnsibleVariableResponse> Variables,
    IReadOnlyList<string> Warnings);
