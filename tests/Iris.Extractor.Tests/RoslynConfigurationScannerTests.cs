using Iris.Extractor.DotNet;

namespace Iris.Extractor.Tests;

public sealed class RoslynConfigurationScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("iris-extractor-roslyn-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Scan_finds_GetValue_GetSection_and_indexer_usages()
    {
        WriteSource("""
            public sealed class Startup
            {
                public Startup(IConfiguration configuration)
                {
                    var mode = configuration.GetValue<string>("Iris:Auth:Mode");
                    var section = configuration.GetSection("Iris:Integrations:Ansible");
                    var host = configuration["AllowedHosts"];
                }
            }
            """);

        var fragment = RoslynConfigurationScanner.Scan(_root);

        Assert.Contains(fragment.ConfigurationKeys, k => k.Key == "Iris:Auth:Mode");
        Assert.Contains(fragment.ConfigurationKeys, k => k.Key == "Iris:Integrations:Ansible");
        Assert.Contains(fragment.ConfigurationKeys, k => k.Key == "AllowedHosts");
        Assert.All(fragment.ConfigurationKeys, k => Assert.Equal("code:IConfiguration", k.TargetKind));
    }

    [Fact]
    public void Scan_treats_GetConnectionString_as_a_secret_key_and_a_database_dependency()
    {
        WriteSource("""
            public sealed class Db
            {
                public Db(IConfiguration configuration)
                {
                    var cs = configuration.GetConnectionString("IrisDb");
                }
            }
            """);

        var fragment = RoslynConfigurationScanner.Scan(_root);

        var key = Assert.Single(fragment.ConfigurationKeys);
        Assert.Equal("ConnectionStrings:IrisDb", key.Key);
        Assert.True(key.Secret);

        var dependency = Assert.Single(fragment.Dependencies);
        Assert.Equal("IrisDb", dependency.Name);
        Assert.Equal("database", dependency.Category);
    }

    [Fact]
    public void Scan_ignores_indexers_on_receivers_that_do_not_look_like_configuration()
    {
        WriteSource("""
            public sealed class Catalog
            {
                public Catalog(System.Collections.Generic.Dictionary<string, string> items)
                {
                    var value = items["not-a-config-key"];
                }
            }
            """);

        var fragment = RoslynConfigurationScanner.Scan(_root);

        Assert.Empty(fragment.ConfigurationKeys);
    }

    private void WriteSource(string content) =>
        File.WriteAllText(Path.Combine(_root, "Source.cs"), content);
}
