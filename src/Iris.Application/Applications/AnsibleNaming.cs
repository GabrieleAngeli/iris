using System.Text;
using System.Text.Json;

namespace Iris.Application.Applications;

/// <summary>
/// Naming conventions shared between the Ansible plan (<see cref="GetApplicationInstallationAnsiblePlanHandler"/>)
/// and the Ansible scaffold generator (<see cref="AnsibleScaffoldGenerator"/>) — kept in one place
/// so the two can never disagree on what an <c>iris_*</c> variable, a <c>.j2</c> template name, or
/// "does this unit need a container/systemd task" resolve to for the same
/// <see cref="Iris.Domain.Applications.ConfigurationKey"/>/<see cref="Iris.Domain.Applications.ApplicationUnitDefinition"/>.
/// </summary>
internal static class AnsibleNaming
{
    private const string AnsibleJ2Prefix = "ansible:j2:";
    private const string AnsibleJ2Bare = "ansible:j2";

    /// <summary>Normalizes a raw <c>TargetKind</c> into the <c>ansible:j2:&lt;target&gt;</c> form the Ansible plan exposes.</summary>
    public static string ToTemplateTarget(string targetKind)
    {
        var clean = targetKind.Trim();
        return clean.StartsWith(AnsibleJ2Bare, StringComparison.OrdinalIgnoreCase)
            ? clean
            : $"{AnsibleJ2Prefix}{clean}";
    }

    public static string StripAnsiblePrefix(string target) =>
        target.StartsWith(AnsibleJ2Prefix, StringComparison.OrdinalIgnoreCase)
            ? target[AnsibleJ2Prefix.Length..]
            : target;

    public static string ToJinjaTemplateName(string target) => $"{StripAnsiblePrefix(target)}.j2";

    public static string ToSafeFileName(string value)
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

    public static string ToAnsibleVariableName(string key)
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

    /// <summary>Whether an application unit's declared execution targets imply a Docker container task.</summary>
    public static bool SupportsDocker(IReadOnlyList<string> executionTargets) =>
        executionTargets.Any(target => target.Contains("docker", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether an application unit implies a systemd/service task — either an explicit
    /// service/systemd execution target, an explicit <c>service</c> kind, or (when no execution
    /// targets are declared at all) the conservative default.
    /// </summary>
    public static bool SupportsService(IReadOnlyList<string> executionTargets, string? kind) =>
        executionTargets.Count == 0 ||
        executionTargets.Any(target =>
            target.Contains("service", StringComparison.OrdinalIgnoreCase) ||
            target.Contains("systemd", StringComparison.OrdinalIgnoreCase)) ||
        string.Equals(kind, "service", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<T> DeserializeList<T>(string? json)
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
}
