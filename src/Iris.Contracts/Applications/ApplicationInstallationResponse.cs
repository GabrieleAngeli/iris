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

public sealed record ApplicationInstallationAnsibleArtifactResponse(
    string? Provider,
    string? Feed,
    string? Name,
    string? Path,
    string? BuildPipelineUrl,
    string? SourceReference);

public sealed record ApplicationInstallationAnsibleAssociationResponse(
    string PlaceholderKey,
    string TargetKind,
    Guid? TargetId,
    string? TargetSlug,
    string Status,
    string? ValuePreview,
    string? Notes);

public sealed record ApplicationInstallationAnsibleOperationInputResponse(
    string Name,
    string? Value);

public sealed record ApplicationInstallationAnsibleOperationResponse(
    int Step,
    string Name,
    string Kind,
    string AnsibleModule,
    string Target,
    string? Template,
    IReadOnlyList<ApplicationInstallationAnsibleOperationInputResponse> Inputs,
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
    ApplicationInstallationAnsibleArtifactResponse Artifact,
    IReadOnlyList<ApplicationInstallationAnsibleAssociationResponse> Associations,
    IReadOnlyList<ApplicationInstallationAnsibleOperationResponse> Operations,
    IReadOnlyList<ApplicationInstallationAnsibleVariableResponse> Variables,
    IReadOnlyList<string> Warnings);

public sealed record ApplicationInstallationAwxLaunchRequest(
    int? JobTemplateId = null,
    string? Inventory = null,
    string? Limit = null,
    bool CheckMode = false);

public sealed record ApplicationInstallationAwxLaunchResponse(
    Guid RunId,
    long JobId,
    string Status,
    string? Url,
    string? Message,
    IReadOnlyDictionary<string, string?> SubmittedVariablesPreview);

/// <summary>One recorded deployment attempt for an installation (an AWX job launch).</summary>
public sealed record InstallationRunResponse(
    Guid Id,
    Guid InstallationId,
    string Kind,
    string Status,
    bool IsTerminal,
    string? ExternalJobId,
    string? ExternalUrl,
    string? Message,
    IReadOnlyDictionary<string, string?> SubmittedVariablesPreview,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc);
