using Iris.Application.Abstractions;
using Iris.Contracts.Applications;

namespace Iris.Infrastructure.Integrations;

internal sealed class AnsibleExecutionPackageBuilder(AnsibleOptions options) : IAnsibleExecutionPackageBuilder, IIntegrationConnector
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
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new IntegrationConnectorStatus(
            Key,
            Name,
            string.IsNullOrWhiteSpace(options.Playbook) ? "Not configured" : "Configured",
            Endpoint,
            $"Playbook: {options.Playbook}"));
}
