using System.Text;
using System.Text.Json;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

public sealed record GetApplicationInstallationAnsiblePlanQuery(Guid InstallationId);

public sealed class GetApplicationInstallationAnsiblePlanHandler(
    IApplicationInstallationRepository installations,
    IApplicationRepository applications,
    IServerRepository servers)
{
    public async Task<ApplicationInstallationAnsiblePlanResponse> HandleAsync(
        GetApplicationInstallationAnsiblePlanQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var installation = await installations.GetAsync(query.InstallationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Application installation", query.InstallationId);
        var application = await applications.GetAsync(installation.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Application", installation.ApplicationId);
        var version = application.Versions.SingleOrDefault(v => v.Id == installation.ApplicationVersionId)
            ?? throw new NotFoundException("Application version", installation.ApplicationVersionId);
        var server = await servers.GetAsync(installation.ServerNodeId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Server", installation.ServerNodeId);

        var selectedProfile = installation.InstallationProfileKey;
        var bindings = installation.Bindings
            .GroupBy(binding => binding.PlaceholderKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        var variables = version.ConfigurationKeys
            .Where(key => IsInSelectedProfile(key.ProfilesJson, selectedProfile))
            .OrderBy(key => key.TargetKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(key => key.Key, StringComparer.OrdinalIgnoreCase)
            .Select(key =>
            {
                var binding = key.PlaceholderKey is null
                    ? null
                    : bindings.GetValueOrDefault(key.PlaceholderKey);
                var source = ResolveSource(key, binding);
                var preview = ResolvePreview(key, binding);
                if (key.Required && binding is null && string.IsNullOrWhiteSpace(key.DefaultValue))
                {
                    warnings.Add($"Required key '{key.Key}' has no binding/default for installation '{installation.Name}'.");
                }

                return new ApplicationInstallationAnsibleVariableResponse(
                    ToAnsibleVariableName(key.PlaceholderKey ?? key.Key),
                    key.Key,
                    key.PlaceholderKey,
                    ToTemplateTarget(key.TargetKind),
                    key.ValueType ?? "string",
                    key.Required,
                    key.Secret,
                    source,
                    key.Secret && binding is null ? null : preview,
                    key.Description);
            })
            .ToArray();

        var targets = variables
            .Select(variable => variable.TargetTemplate)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(target => target, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var unit = string.IsNullOrWhiteSpace(installation.ApplicationUnitKey)
            ? null
            : version.ApplicationUnits.SingleOrDefault(item =>
                string.Equals(item.Key, installation.ApplicationUnitKey, StringComparison.OrdinalIgnoreCase));
        var artifact = new ApplicationInstallationAnsibleArtifactResponse(
            application.ArtifactProvider,
            application.ArtifactFeed,
            application.ArtifactName,
            unit?.ArtifactPath ?? application.ArtifactPath,
            application.BuildPipelineUrl,
            version.SourceReference);
        var associations = installation.Bindings
            .OrderBy(binding => binding.PlaceholderKey, StringComparer.OrdinalIgnoreCase)
            .Select(binding => new ApplicationInstallationAnsibleAssociationResponse(
                binding.PlaceholderKey,
                binding.TargetKind,
                binding.TargetId,
                binding.TargetSlug,
                binding.TargetId is null && string.IsNullOrWhiteSpace(binding.TargetSlug) ? "unresolved" : "resolved",
                binding.ValuePreview,
                binding.Notes))
            .ToArray();
        var operations = BuildOperations(installation.Id, application.Slug, version, unit, server.Name, targets, artifact);

        warnings.Add("Iris does not render final configuration files directly: Ansible must consume these variables in Jinja2 templates and deploy the rendered files per installation instance.");

        return new ApplicationInstallationAnsiblePlanResponse(
            installation.Id,
            installation.Name,
            application.Slug,
            version.Version,
            installation.ApplicationUnitKey,
            installation.InstallationProfileKey,
            installation.Environment.ToString(),
            server.Name,
            targets,
            artifact,
            associations,
            operations,
            variables,
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool IsInSelectedProfile(string? profilesJson, string? selectedProfile)
    {
        if (string.IsNullOrWhiteSpace(selectedProfile) || string.IsNullOrWhiteSpace(profilesJson))
        {
            return true;
        }

        try
        {
            var profiles = JsonSerializer.Deserialize<IReadOnlyList<string>>(profilesJson) ?? [];
            return profiles.Count == 0 || profiles.Contains(selectedProfile, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static string ResolveSource(ConfigurationKey key, ApplicationInstallationBinding? binding)
    {
        if (binding is not null)
        {
            return binding.TargetKind switch
            {
                ApplicationInstallationTargetKinds.DataService => "iris:data-service",
                ApplicationInstallationTargetKinds.Application => "iris:application",
                _ => $"iris:{binding.TargetKind}"
            };
        }

        return string.IsNullOrWhiteSpace(key.DefaultValue)
            ? "manual"
            : "manifest:default";
    }

    private static string? ResolvePreview(ConfigurationKey key, ApplicationInstallationBinding? binding) =>
        binding?.ValuePreview ?? key.DefaultValue;

    private static IReadOnlyList<ApplicationInstallationAnsibleOperationResponse> BuildOperations(
        Guid installationId,
        string applicationSlug,
        ApplicationVersion version,
        ApplicationUnitDefinition? unit,
        string serverName,
        IReadOnlyList<string> templateTargets,
        ApplicationInstallationAnsibleArtifactResponse artifact)
    {
        var operations = new List<ApplicationInstallationAnsibleOperationResponse>
        {
            new(
                1,
                "Load Iris deployment plan",
                "iris.plan",
                "ansible.builtin.uri",
                $"/applications/installations/{installationId}/ansible-vars",
                null,
                [new("installationId", installationId.ToString())],
                "AWX/Ansible reads the Iris plan before changing the target host.")
        };

        var step = 2;
        if (!string.IsNullOrWhiteSpace(artifact.Path) || !string.IsNullOrWhiteSpace(artifact.Name))
        {
            operations.Add(new ApplicationInstallationAnsibleOperationResponse(
                step++,
                "Fetch deployable artifact",
                "artifact.fetch",
                "ansible.builtin.get_url/copy/unarchive",
                serverName,
                null,
                [
                    new("provider", artifact.Provider),
                    new("feed", artifact.Feed),
                    new("name", artifact.Name),
                    new("path", artifact.Path),
                    new("sourceReference", artifact.SourceReference)
                ],
                "The Ansible role decides whether the artifact is downloaded, copied or unpacked."));
        }

        foreach (var target in templateTargets)
        {
            operations.Add(new ApplicationInstallationAnsibleOperationResponse(
                step++,
                $"Render {StripAnsiblePrefix(target)}",
                "configuration.render",
                "ansible.builtin.template",
                serverName,
                ToJinjaTemplateName(target),
                [new("templateTarget", target)],
                "The final configuration file is rendered from a versioned Jinja2 template on the target host."));
        }

        if (unit is not null)
        {
            var executionTargets = DeserializeList<string>(unit.ExecutionTargetsJson);
            var supportsDocker = executionTargets.Any(target => target.Contains("docker", StringComparison.OrdinalIgnoreCase));
            var supportsService = executionTargets.Count == 0 ||
                executionTargets.Any(target =>
                    target.Contains("service", StringComparison.OrdinalIgnoreCase) ||
                    target.Contains("systemd", StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(unit.Kind, "service", StringComparison.OrdinalIgnoreCase);

            if (supportsDocker)
            {
                operations.Add(new ApplicationInstallationAnsibleOperationResponse(
                    step++,
                    $"Deploy container {unit.Key}",
                    "runtime.container",
                    "community.docker.docker_container",
                    serverName,
                    null,
                    [new("applicationUnit", unit.Key), new("artifact", artifact.Path), new("entryPoint", unit.EntryPoint)],
                    "Ansible owns container creation/update and restart policy."));
            }

            if (supportsService)
            {
                operations.Add(new ApplicationInstallationAnsibleOperationResponse(
                    step++,
                    $"Manage service {unit.Key}",
                    "runtime.service",
                    "ansible.builtin.systemd_service",
                    serverName,
                    $"systemd/{ToSafeFileName(unit.Key)}.service.j2",
                    [new("applicationUnit", unit.Key), new("entryPoint", unit.EntryPoint), new("artifact", artifact.Path)],
                    "Ansible owns service file rendering, enablement and restart."));
            }
        }

        var requiredPorts = version.RuntimeMetadata.RequiredPorts;
        var portKeys = DeserializeList<string>(version.RuntimeMetadata.PortKeysJson);
        if (requiredPorts.Count > 0 || portKeys.Count > 0)
        {
            operations.Add(new ApplicationInstallationAnsibleOperationResponse(
                step,
                "Apply network exposure",
                "network.apply",
                "role:iris.firewall_proxy",
                serverName,
                null,
                [
                    new("requiredPorts", string.Join(",", requiredPorts)),
                    new("portKeys", string.Join(",", portKeys))
                ],
                "Firewall, reverse proxy, TLS and DNS changes are performed by Ansible roles, not by Iris."));
        }

        return operations;
    }

    private static string ToTemplateTarget(string targetKind)
    {
        var clean = targetKind.Trim();
        return clean.StartsWith("ansible:j2", StringComparison.OrdinalIgnoreCase)
            ? clean
            : $"ansible:j2:{clean}";
    }

    private static string StripAnsiblePrefix(string target) =>
        target.StartsWith("ansible:j2:", StringComparison.OrdinalIgnoreCase)
            ? target["ansible:j2:".Length..]
            : target;

    private static string ToJinjaTemplateName(string target) => $"{StripAnsiblePrefix(target)}.j2";

    private static string ToSafeFileName(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '-');
        }

        return builder.ToString();
    }

    private static IReadOnlyList<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<T>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ToAnsibleVariableName(string key)
    {
        var builder = new StringBuilder("iris_");
        var previousWasSeparator = false;
        foreach (var character in key.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator)
            {
                builder.Append('_');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().TrimEnd('_');
    }
}
