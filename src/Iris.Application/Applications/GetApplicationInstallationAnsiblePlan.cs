using System.Text;
using System.Text.Json;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Applications;

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
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

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

    private static string ToTemplateTarget(string targetKind)
    {
        var clean = targetKind.Trim();
        return clean.StartsWith("ansible:j2", StringComparison.OrdinalIgnoreCase)
            ? clean
            : $"ansible:j2:{clean}";
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
