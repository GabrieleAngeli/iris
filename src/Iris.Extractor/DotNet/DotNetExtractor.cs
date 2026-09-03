using Iris.Contracts.Applications;

namespace Iris.Extractor.DotNet;

/// <summary>Runs every .NET scanner against a source tree and merges the results into one package,
/// matching the shape <c>POST /applications/{id}/versions/{id}/import</c> accepts.</summary>
internal static class DotNetExtractor
{
    public static ImportConfigurationPackageRequest Extract(string root, string schemaVersion)
    {
        var appSettings = AppSettingsScanner.Scan(root);
        var codeUsages = RoslynConfigurationScanner.Scan(root);
        var ports = LaunchSettingsScanner.Scan(root);

        var warnings = new List<string>();
        if (ports.Count > 0)
        {
            warnings.Add(
                $"Detected port(s) {string.Join(", ", ports)} in launchSettings.json. " +
                "RuntimeMetadata.RequiredPorts is only set when the application version is created " +
                "(POST /applications/{applicationId}/versions) and cannot be updated via /import — " +
                "set it there if these ports matter for placement.");
        }

        return new ImportConfigurationPackageRequest(
            schemaVersion,
            MergeConfigurationKeys(appSettings.ConfigurationKeys, codeUsages.ConfigurationKeys),
            MergeDependencies(appSettings.Dependencies, codeUsages.Dependencies),
            Placeholders: [],
            Warnings: warnings);
    }

    /// <summary>File-declared keys win on conflict — they carry a concrete <c>DefaultValue</c> and a
    /// precise <c>TargetKind</c>, whereas code-discovered keys are just evidence the app reads them.</summary>
    private static IReadOnlyList<ConfigurationKeyInput> MergeConfigurationKeys(
        IReadOnlyList<ConfigurationKeyInput> fromFiles,
        IReadOnlyList<ConfigurationKeyInput> fromCode)
    {
        var merged = new Dictionary<string, ConfigurationKeyInput>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in fromFiles)
        {
            merged[key.Key] = key;
        }

        foreach (var key in fromCode)
        {
            merged.TryAdd(key.Key, key);
        }

        return merged.Values.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<DependencyInput> MergeDependencies(
        IReadOnlyList<DependencyInput> fromFiles,
        IReadOnlyList<DependencyInput> fromCode)
    {
        var merged = new Dictionary<string, DependencyInput>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in fromFiles)
        {
            merged[dependency.Name] = dependency;
        }

        foreach (var dependency in fromCode)
        {
            merged.TryAdd(dependency.Name, dependency);
        }

        return merged.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
