namespace Iris.Contracts.Applications;

public sealed record ApplicationInstallationBindingInput(
    string PlaceholderKey,
    string TargetKind,
    Guid? TargetId,
    string? TargetSlug = null,
    string? ValuePreview = null,
    string? Notes = null);

public sealed record CreateApplicationInstallationRequest(
    string Name,
    Guid ApplicationVersionId,
    Guid ServerNodeId,
    Guid CustomerContextId,
    string? ApplicationUnitKey = null,
    string? InstallationProfileKey = null,
    string? Notes = null,
    IReadOnlyList<ApplicationInstallationBindingInput>? Bindings = null);
