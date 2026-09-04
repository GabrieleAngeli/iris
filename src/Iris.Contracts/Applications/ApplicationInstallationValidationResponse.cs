namespace Iris.Contracts.Applications;

/// <summary>
/// One check produced by the deployment Validation Engine for an
/// <c>ApplicationInstallation</c>. <see cref="Severity"/> is <c>error</c> (blocks the
/// deployment), <c>warning</c> (deploy at your own risk) or <c>info</c> (context Iris
/// cannot verify yet). <see cref="Category"/> groups checks:
/// <c>placeholder</c>, <c>configuration</c>, <c>dependency</c>, <c>os</c>,
/// <c>capability</c>, <c>port</c>, <c>capacity</c>, <c>constraint</c>.
/// </summary>
public sealed record ApplicationInstallationValidationCheckResponse(
    string Code,
    string Severity,
    string Category,
    string Target,
    string Message);

public sealed record ApplicationInstallationValidationResponse(
    Guid InstallationId,
    string InstallationName,
    string ApplicationSlug,
    string ApplicationVersion,
    string? ApplicationUnitKey,
    string? InstallationProfileKey,
    string ServerName,
    string Environment,
    bool IsValid,
    int Errors,
    int Warnings,
    int Infos,
    IReadOnlyList<ApplicationInstallationValidationCheckResponse> Checks);
