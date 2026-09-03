using Iris.Extractor.DotNet;

namespace Iris.Extractor.Tests;

public sealed class AppSettingsScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("iris-extractor-appsettings-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Scan_flattens_nested_json_into_dotted_keys()
    {
        File.WriteAllText(Path.Combine(_root, "appsettings.json"), """
            {
              "AllowedHosts": "*",
              "Serilog": { "MinimumLevel": { "Default": "Information" } }
            }
            """);

        var fragment = AppSettingsScanner.Scan(_root);

        Assert.Contains(fragment.ConfigurationKeys, k => k.Key == "AllowedHosts" && k.DefaultValue == "*");
        Assert.Contains(fragment.ConfigurationKeys, k => k.Key == "Serilog:MinimumLevel:Default" && k.DefaultValue == "Information");
        Assert.All(fragment.ConfigurationKeys, k => Assert.Equal("appsettings.json", k.TargetKind));
    }

    [Fact]
    public void Scan_marks_connection_strings_secret_and_promotes_them_to_a_dependency()
    {
        File.WriteAllText(Path.Combine(_root, "appsettings.json"), """
            { "ConnectionStrings": { "IrisDb": "Data Source=iris.db" } }
            """);

        var fragment = AppSettingsScanner.Scan(_root);

        var key = Assert.Single(fragment.ConfigurationKeys);
        Assert.Equal("ConnectionStrings:IrisDb", key.Key);
        Assert.True(key.Secret);
        Assert.Null(key.DefaultValue);

        var dependency = Assert.Single(fragment.Dependencies);
        Assert.Equal("IrisDb", dependency.Name);
        Assert.Equal("database", dependency.Category);
    }

    [Fact]
    public void Scan_applies_the_secret_heuristic_to_ordinary_keys()
    {
        File.WriteAllText(Path.Combine(_root, "appsettings.json"), """
            { "Mail": { "SmtpPassword": "unused-in-source-control" } }
            """);

        var fragment = AppSettingsScanner.Scan(_root);

        var key = Assert.Single(fragment.ConfigurationKeys);
        Assert.True(key.Secret);
        Assert.Null(key.DefaultValue);
    }

    [Fact]
    public void Scan_ignores_files_under_bin_and_obj()
    {
        var binDir = Path.Combine(_root, "bin", "Debug");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(Path.Combine(binDir, "appsettings.json"), """{ "Ignored": "yes" }""");

        var fragment = AppSettingsScanner.Scan(_root);

        Assert.Empty(fragment.ConfigurationKeys);
    }
}
