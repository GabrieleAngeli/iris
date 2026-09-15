using System.Text.Json;
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

public sealed record AwxJobStatusResult(
    string Status,
    bool Finished,
    bool Succeeded,
    string? Url,
    string? Message);

/// <summary>Result of looking up a host's cached <c>ansible_facts</c> by name. AWX's fact
/// cache (a Job Template run with <c>use_fact_cache=True</c>) is per-host, keyed by the host's
/// name in AWX's own Inventory — <see cref="HostFound"/> is <c>false</c> when no such host
/// exists there yet.</summary>
public sealed record AwxHostFactsResult(
    bool HostFound,
    IReadOnlyDictionary<string, JsonElement>? Facts);

public interface IAwxClient
{
    Task<AwxJobLaunchResult> LaunchAsync(
        AwxJobLaunch launch,
        CancellationToken cancellationToken = default);

    /// <summary>Polls the executor for the current state of a previously launched job.</summary>
    Task<AwxJobStatusResult> GetJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    /// <summary>Looks the host up by name in AWX's Inventory, then reads its cached
    /// <c>ansible_facts</c> (populated by the last job run against it with
    /// <c>use_fact_cache=True</c>).</summary>
    Task<AwxHostFactsResult> GetHostFactsAsync(
        string hostname,
        CancellationToken cancellationToken = default);
}
