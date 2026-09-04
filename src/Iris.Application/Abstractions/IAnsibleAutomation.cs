using Iris.Contracts.Applications;

namespace Iris.Application.Abstractions;

public sealed record AnsibleExecutionPackage(
    string Playbook,
    string? Inventory,
    string? Limit,
    bool CheckMode,
    IReadOnlyDictionary<string, object?> ExtraVars);

public interface IAnsibleExecutionPackageBuilder
{
    AnsibleExecutionPackage Build(
        ApplicationInstallationAnsiblePlanResponse plan,
        ApplicationInstallationAwxLaunchRequest request);
}

public sealed record AwxJobLaunch(
    int? JobTemplateId,
    AnsibleExecutionPackage Package);

public sealed record AwxJobLaunchResult(
    long JobId,
    string Status,
    string? Url,
    string? Message);

public interface IAwxClient
{
    Task<AwxJobLaunchResult> LaunchAsync(
        AwxJobLaunch launch,
        CancellationToken cancellationToken = default);
}
