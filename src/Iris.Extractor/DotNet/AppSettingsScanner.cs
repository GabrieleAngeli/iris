using System.Text.Json;
using Iris.Contracts.Applications;

namespace Iris.Extractor.DotNet;

/// <summary>Flattens <c>appsettings*.json</c> into configuration keys, promoting <c>ConnectionStrings:*</c>
/// entries into dependencies as well (matching the "database" convention used elsewhere in Iris).</summary>
internal static class AppSettingsScanner
{
    public static ConfigurationFragment Scan(string root)
    {
        var configurationKeys = new List<ConfigurationKeyInput>();
        var dependencies = new List<DependencyInput>();

        foreach (var file in FindAppSettingsFiles(root))
        {
            var targetKind = Path.GetFileName(file);

            using var stream = File.OpenRead(file);
            using var document = JsonDocument.Parse(stream);

            foreach (var (key, value) in Flatten(document.RootElement))
            {
                var secret = SecretHeuristics.LooksSecret(key);
                configurationKeys.Add(new ConfigurationKeyInput(
                    key,
                    targetKind,
                    Required: true,
                    Secret: secret,
                    DefaultValue: secret ? null : value,
                    Description: null,
                    Purpose: null,
                    PlaceholderKey: null));

                if (key.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase))
                {
                    var name = key["ConnectionStrings:".Length..];
                    dependencies.Add(new DependencyInput(
                        name,
                        Category: "database",
                        Required: true,
                        Description: $"Discovered from {targetKind}.",
                        PlaceholderKey: null));
                }
            }
        }

        return new ConfigurationFragment(configurationKeys, dependencies);
    }

    private static IEnumerable<string> FindAppSettingsFiles(string root) =>
        Directory.EnumerateFiles(root, "appsettings*.json", SearchOption.AllDirectories)
            .Where(path => !PathFiltering.IsExcluded(root, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<(string Key, string? Value)> Flatten(JsonElement element, string prefix = "")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = prefix.Length == 0 ? property.Name : $"{prefix}:{property.Name}";
                    foreach (var flattened in Flatten(property.Value, key))
                    {
                        yield return flattened;
                    }
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var flattened in Flatten(item, $"{prefix}:{index}"))
                    {
                        yield return flattened;
                    }

                    index++;
                }

                break;

            default:
                if (prefix.Length > 0)
                {
                    yield return (prefix, element.ValueKind == JsonValueKind.Null ? null : element.ToString());
                }

                break;
        }
    }
}
