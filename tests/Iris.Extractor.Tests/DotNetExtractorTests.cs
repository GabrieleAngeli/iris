using System.Text.Json;
using Iris.Contracts.Applications;
using Iris.Extractor.DotNet;

namespace Iris.Extractor.Tests;

public sealed class DotNetExtractorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("iris-extractor-dotnet-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Extract_prefers_the_appsettings_entry_when_a_key_is_seen_in_both_file_and_code()
    {
        File.WriteAllText(Path.Combine(_root, "appsettings.json"), """
            { "Iris": { "Auth": { "Mode": "Composite" } } }
            """);
        File.WriteAllText(Path.Combine(_root, "Startup.cs"), """
            public sealed class Startup
            {
                public Startup(IConfiguration configuration)
                {
                    var mode = configuration.GetValue<string>("Iris:Auth:Mode");
                }
            }
            """);

        var package = DotNetExtractor.Extract(_root, "1.0");

        var key = Assert.Single(package.ConfigurationKeys, k => k.Key == "Iris:Auth:Mode");
        Assert.Equal("appsettings.json", key.TargetKind);
        Assert.Equal("Composite", key.DefaultValue);
    }

    [Fact]
    public void Extract_surfaces_launchSettings_ports_as_a_warning_instead_of_a_configuration_key()
    {
        var propertiesDir = Path.Combine(_root, "Properties");
        Directory.CreateDirectory(propertiesDir);
        File.WriteAllText(Path.Combine(propertiesDir, "launchSettings.json"), """
            {
              "profiles": {
                "https": { "applicationUrl": "https://localhost:7169;http://localhost:5006" }
              }
            }
            """);

        var package = DotNetExtractor.Extract(_root, "1.0");

        Assert.Empty(package.ConfigurationKeys);
        var warning = Assert.Single(package.Warnings ?? []);
        Assert.Contains("5006", warning);
        Assert.Contains("7169", warning);
    }

    [Fact]
    public void Extract_produces_a_package_that_round_trips_through_the_real_import_contract()
    {
        File.WriteAllText(Path.Combine(_root, "appsettings.json"), """
            { "ConnectionStrings": { "IrisDb": "Data Source=iris.db" } }
            """);

        var package = DotNetExtractor.Extract(_root, "1.0");

        var json = JsonSerializer.Serialize(package, PackageJsonOptions.Instance);
        var roundTripped = JsonSerializer.Deserialize<ImportConfigurationPackageRequest>(json, PackageJsonOptions.Instance);

        Assert.NotNull(roundTripped);
        Assert.Equal(package.SchemaVersion, roundTripped.SchemaVersion);
        Assert.Equal(package.ConfigurationKeys.Count, roundTripped.ConfigurationKeys.Count);
        Assert.Equal(package.Dependencies.Single().Name, roundTripped.Dependencies.Single().Name);
    }
}
