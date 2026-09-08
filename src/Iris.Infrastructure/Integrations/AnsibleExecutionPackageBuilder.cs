using Iris.Application.Abstractions;
using Iris.Contracts.Applications;
using Iris.Infrastructure.Processes;

namespace Iris.Infrastructure.Integrations;

internal sealed class AnsibleExecutionPackageBuilder(AnsibleOptions options, IProcessRunner processRunner)
    : IAnsibleExecutionPackageBuilder, IIntegrationConnector
{
    private const string AnsiblePlaybookExecutable = "ansible-playbook";

    public string Key => "ansible";

    public string Name => "Ansible";

    public string? Endpoint => options.Endpoint;

    public AnsibleExecutionPackage Build(
        ApplicationInstallationAnsiblePlanResponse plan,
        ApplicationInstallationAwxLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(request);

        var variables = plan.Variables.ToDictionary(
            variable => variable.Name,
            variable => (object?)variable.ValuePreview,
            StringComparer.OrdinalIgnoreCase);

        var extraVars = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["iris_installation_id"] = plan.InstallationId.ToString(),
            ["iris_installation_name"] = plan.InstallationName,
            ["iris_application_slug"] = plan.ApplicationSlug,
            ["iris_application_version"] = plan.ApplicationVersion,
            ["iris_application_unit_key"] = plan.ApplicationUnitKey,
            ["iris_installation_profile_key"] = plan.InstallationProfileKey,
            ["iris_environment"] = plan.Environment,
            ["iris_server_name"] = plan.ServerName,
            ["iris_template_targets"] = plan.TemplateTargets,
            ["iris_variables"] = variables,
            ["iris_associations"] = plan.Associations,
            ["iris_operations"] = plan.Operations,
            ["iris_artifact"] = plan.Artifact,
            ["iris_warnings"] = plan.Warnings
        };

        foreach (var variable in variables)
        {
            extraVars[variable.Key] = variable.Value;
        }

        return new AnsibleExecutionPackage(
            options.Playbook,
            string.IsNullOrWhiteSpace(request.Inventory) ? options.Inventory : request.Inventory,
            request.Limit,
            request.CheckMode,
            extraVars);
    }

    public async Task<IntegrationConnectorStatus> GetStatusAsync(
        bool probe = false,
        CancellationToken cancellationToken = default)
    {
        // "Configured" reflects the endpoint, same convention as OpenBao/AWX — not the
        // playbook, which always carries a non-blank default ("iris-deploy-application.yml")
        // and so used to report "Configured" even on a completely untouched install. Real bug
        // found via manual testing (2026-09-08): this made Ansible look set up when nothing had
        // ever been configured.
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return new IntegrationConnectorStatus(Key, Name, "Not configured", null, "Endpoint is required.");
        }

        if (!probe)
        {
            return new IntegrationConnectorStatus(Key, Name, "Configured", Endpoint, $"Playbook: {options.Playbook}");
        }

        // There's nothing to reach over HTTP here — Ansible in this design (see the Fase 3
        // plan) runs as a local CLI invocation, not a remote API Iris calls. The one real,
        // honest thing a probe can check today is whether `ansible-playbook` itself is
        // installed and runnable on this host, since that's the actual prerequisite for every
        // future launch — a static "Configured" that never changed on Test (the bug reported
        // 2026-09-08: "cliccando test non succede nulla") gave the operator no way to tell.
        // IProcessRunner never throws for a missing executable (see SystemProcessRunner) — it
        // just comes back with a non-zero/-1 exit code, so no try/catch is needed here.
        var result = await processRunner
            .RunAsync(AnsiblePlaybookExecutable, ["--version"], cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0
            ? new IntegrationConnectorStatus(Key, Name, "Reachable", Endpoint, result.StandardOutput.Trim().Split('\n').FirstOrDefault())
            : new IntegrationConnectorStatus(Key, Name, "Unreachable", Endpoint, "ansible-playbook is not runnable on this host.");
    }
}
