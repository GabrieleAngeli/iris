using System.Net;
using System.Net.Http.Json;

namespace Iris.Api.Tests;

public sealed class ApplicationsApiTests(IrisApiFactory factory) : IClassFixture<IrisApiFactory>
{
    private HttpClient Admin()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "admin@iris.local");
        return client;
    }

    private HttpClient Reader()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "gio@globex.example");
        return client;
    }

    [Fact]
    public async Task Reader_cannot_create_an_application()
    {
        var response = await Reader().PostAsJsonAsync("/applications", new
        {
            name = "nope",
            runtimeType = "CSharp",
            repositoryUrl = "https://git.example/nope",
            defaultBranch = "main",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reader_cannot_update_an_application()
    {
        var admin = Admin();
        var name = "svc-" + Guid.NewGuid().ToString("N")[..8];
        var create = await admin.PostAsJsonAsync("/applications", new
        {
            name,
            runtimeType = "CSharp",
            repositoryUrl = $"https://git.example/{name}",
            defaultBranch = "main",
        });
        var application = await create.Content.ReadFromJsonAsync<ApplicationDto>();

        var update = await Reader().PutAsJsonAsync($"/applications/{application!.Id}", new
        {
            name = "changed",
            runtimeType = "Docker",
            repositoryUrl = "https://git.example/changed",
            defaultBranch = "release/main",
            isActive = true,
        });

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
    }

    [Fact]
    public async Task Admin_can_update_application_inventory()
    {
        var admin = Admin();
        var name = "svc-" + Guid.NewGuid().ToString("N")[..8];
        var create = await admin.PostAsJsonAsync("/applications", new
        {
            name,
            runtimeType = "CSharp",
            repositoryUrl = $"https://git.example/{name}",
            defaultBranch = "main",
        });
        var application = await create.Content.ReadFromJsonAsync<ApplicationDto>();

        var update = await admin.PutAsJsonAsync($"/applications/{application!.Id}", new
        {
            name = $"{name}-renamed",
            runtimeType = "Docker",
            repositoryUrl = $"https://git.example/{name}-renamed",
            defaultBranch = "release/main",
            description = "Runtime inventory updated from the app catalog.",
            isActive = false,
        });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<ApplicationDto>();
        Assert.Equal($"{name}-renamed", updated!.Name);
        Assert.Equal(application.Slug, updated.Slug);
        Assert.Equal("Docker", updated.RuntimeType);
        Assert.Equal($"https://git.example/{name}-renamed", updated.RepositoryUrl);
        Assert.Equal("release/main", updated.DefaultBranch);
        Assert.Equal("Runtime inventory updated from the app catalog.", updated.Description);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task Admin_can_create_an_application_add_a_version_and_import_its_configuration_knowledge()
    {
        var admin = Admin();
        var name = "svc-" + Guid.NewGuid().ToString("N")[..8];

        var create = await admin.PostAsJsonAsync("/applications", new
        {
            name,
            runtimeType = "CSharp",
            repositoryUrl = $"https://git.example/{name}",
            defaultBranch = "main",
            artifactProvider = "Nexus",
            artifactFeed = "iris/releases",
            artifactName = $"{name}.zip",
            artifactPath = $"drop/{name}.zip",
            buildPipelineUrl = $"https://dev.azure.com/org/project/_build?definitionId={name}",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var application = await create.Content.ReadFromJsonAsync<ApplicationDto>();
        Assert.Empty(application!.Versions);
        Assert.Equal("Nexus", application.ArtifactProvider);
        Assert.Equal($"drop/{name}.zip", application.ArtifactPath);

        var addVersion = await admin.PostAsJsonAsync($"/applications/{application.Id}/versions", new
        {
            version = "1.0.0",
            sourceReference = "git-sha:abc123",
            runtimeMetadata = new
            {
                runtimeName = "dotnet9",
                preferredOs = "Linux",
                requiredCpuCores = 2,
                requiredMemoryMb = 1024,
                requiredPorts = new[] { 8080, 8443 },
            },
        });
        Assert.Equal(HttpStatusCode.Created, addVersion.StatusCode);
        var version = await addVersion.Content.ReadFromJsonAsync<VersionSummaryDto>();
        Assert.Equal("1.0.0", version!.Version);
        Assert.Equal("dotnet9", version.RuntimeMetadata.RuntimeName);

        // duplicate version -> 409
        var dup = await admin.PostAsJsonAsync($"/applications/{application.Id}/versions", new
        {
            version = "1.0.0",
            runtimeMetadata = new { runtimeName = "dotnet9", requiredPorts = Array.Empty<int>() },
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        var import = await admin.PostAsJsonAsync(
            $"/applications/{application.Id}/versions/{version.Id}/import", new
            {
                schemaVersion = "1.0",
                configurationKeys = new[]
                {
                    new
                    {
                        key = "ConnectionStrings:Main",
                        targetKind = "appsettings.json",
                        required = true,
                        secret = true,
                        placeholderKey = "domain.db.main.connectionString",
                    },
                },
                dependencies = new[]
                {
                    new
                    {
                        name = "postgres",
                        category = "database",
                        required = true,
                        placeholderKey = "domain.db.main",
                        providerApplicationSlug = "orders-api",
                        providerPlaceholderKey = "domain.db.main.connectionString",
                    },
                },
                placeholders = new[]
                {
                    new { key = "domain.db.main.connectionString", category = "database", required = true },
                },
                warnings = new[] { "Unresolved placeholder: domain.cache.redis" },
            });
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var detail = await import.Content.ReadFromJsonAsync<VersionDetailDto>();
        Assert.Single(detail!.ConfigurationKeys);
        var dependency = Assert.Single(detail.Dependencies);
        Assert.Equal("orders-api", dependency.ProviderApplicationSlug);
        Assert.Equal("domain.db.main.connectionString", dependency.ProviderPlaceholderKey);
        Assert.Single(detail.Placeholders);
        Assert.Single(detail.ImportWarnings);

        // reading it back gives the same snapshot
        var getDetail = await admin.GetFromJsonAsync<VersionDetailDto>(
            $"/applications/{application.Id}/versions/{version.Id}");
        Assert.Single(getDetail!.ConfigurationKeys);

        // the catalog lists it with summarised counts
        var list = await admin.GetFromJsonAsync<List<ApplicationDto>>("/applications");
        var listed = Assert.Single(list!, a => a.Id == application.Id);
        var listedVersion = Assert.Single(listed.Versions);
        Assert.Equal(1, listedVersion.ConfigurationKeyCount);

        // a reimport replaces the previous snapshot rather than accumulating it
        var reimport = await admin.PostAsJsonAsync(
            $"/applications/{application.Id}/versions/{version.Id}/import", new
            {
                schemaVersion = "1.1",
                configurationKeys = Array.Empty<object>(),
                dependencies = Array.Empty<object>(),
                placeholders = Array.Empty<object>(),
            });
        Assert.Equal(HttpStatusCode.OK, reimport.StatusCode);
        var reimported = await reimport.Content.ReadFromJsonAsync<VersionDetailDto>();
        Assert.Empty(reimported!.ConfigurationKeys);
        Assert.Empty(reimported.ImportWarnings);
    }

    [Fact]
    public async Task Importing_into_an_unknown_version_returns_not_found()
    {
        var admin = Admin();
        var name = "svc-" + Guid.NewGuid().ToString("N")[..8];

        var create = await admin.PostAsJsonAsync("/applications", new
        {
            name,
            runtimeType = "Node",
            repositoryUrl = $"https://git.example/{name}",
            defaultBranch = "main",
        });
        var application = await create.Content.ReadFromJsonAsync<ApplicationDto>();

        var import = await admin.PostAsJsonAsync(
            $"/applications/{application!.Id}/versions/{Guid.NewGuid()}/import", new
            {
                schemaVersion = "1.0",
                configurationKeys = Array.Empty<object>(),
                dependencies = Array.Empty<object>(),
                placeholders = Array.Empty<object>(),
            });
        Assert.Equal(HttpStatusCode.NotFound, import.StatusCode);
    }

    private sealed record RuntimeMetadataDto(string RuntimeName, string? PreferredOs, int? RequiredCpuCores, int? RequiredMemoryMb, List<int> RequiredPorts);

    private sealed record VersionSummaryDto(
        Guid Id, string Version, string? SourceReference, RuntimeMetadataDto RuntimeMetadata,
        int ConfigurationKeyCount, int DependencyCount, int PlaceholderCount, DateTimeOffset? LastImportedAtUtc);

    private sealed record ApplicationDto(
        Guid Id,
        string Name,
        string Slug,
        string RuntimeType,
        string RepositoryUrl,
        string DefaultBranch,
        string? Description,
        string? ArtifactProvider,
        string? ArtifactFeed,
        string? ArtifactName,
        string? ArtifactPath,
        string? BuildPipelineUrl,
        bool IsActive,
        List<VersionSummaryDto> Versions);

    private sealed record ConfigKeyDto(Guid Id, string Key);

    private sealed record DependencyDto(Guid Id, string Name, string? ProviderApplicationSlug, string? ProviderPlaceholderKey);

    private sealed record PlaceholderDto(Guid Id, string Key);

    private sealed record VersionDetailDto(
        Guid Id, Guid ApplicationId, string Version, List<ConfigKeyDto> ConfigurationKeys,
        List<DependencyDto> Dependencies, List<PlaceholderDto> Placeholders, List<string> ImportWarnings);
}
