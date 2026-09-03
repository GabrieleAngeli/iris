using System.Text.Json;

namespace Iris.Extractor.DotNet;

/// <summary>Reads the ports declared in <c>Properties/launchSettings.json</c>. These have no home in
/// the import package today (<c>RuntimeMetadata.RequiredPorts</c> is set only when the application
/// version is created) — <see cref="DotNetExtractor"/> surfaces them as a warning instead.</summary>
internal static class LaunchSettingsScanner
{
    public static IReadOnlyList<int> Scan(string root)
    {
        var ports = new SortedSet<int>();

        foreach (var file in Directory.EnumerateFiles(root, "launchSettings.json", SearchOption.AllDirectories))
        {
            if (PathFiltering.IsExcluded(root, file))
            {
                continue;
            }

            using var stream = File.OpenRead(file);
            using var document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty("profiles", out var profiles) ||
                profiles.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var profile in profiles.EnumerateObject())
            {
                if (!profile.Value.TryGetProperty("applicationUrl", out var applicationUrl) ||
                    applicationUrl.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                foreach (var url in (applicationUrl.GetString() ?? string.Empty)
                             .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
                    {
                        ports.Add(parsed.Port);
                    }
                }
            }
        }

        return ports.ToArray();
    }
}
