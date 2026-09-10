using Iris.Application.Abstractions;
using Iris.Contracts.Applications;

namespace Iris.Infrastructure.Integrations;

internal sealed class AnsibleExecutionPackageBuilder(AnsibleOptions options)
    : IAnsibleExecutionPackageBuilder, IIntegrationConnector
{
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

    public Task<IntegrationConnectorStatus> GetStatusAsync(
        bool probe = false,
        CancellationToken cancellationToken = default)
    {
        // Iris never runs `ansible-playbook` itself — Build() only assembles the extra_vars the
        // AWX launch sends, and AWX drives the one real Ansible on the ops server. So the normal
        // state here is "managed via AWX", not a separate thing to health-check. An explicit
        // endpoint is the opt-in for future direct calls; even then Iris does not probe it
        // (there is no standard Ansible health endpoint, and a fake check is worse than none).
        var status = string.IsNullOrWhiteSpace(options.Endpoint)
            ? new IntegrationConnectorStatus(
                Key,
                Name,
                "Managed via AWX",
                null,
                "Ansible runs through AWX; Iris never calls it directly. Set an endpoint only for direct calls.")
            : new IntegrationConnectorStatus(
                Key,
                Name,
                "Configured",
                Endpoint,
                "Direct Ansible endpoint set — Iris does not health-check it.");

        return Task.FromResult(status);
    }
}
