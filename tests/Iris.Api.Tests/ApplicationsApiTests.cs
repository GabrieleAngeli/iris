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
    public async Task Admin_can_generate_an_ansible_scaffold_for_a_version()
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

        var addVersion = await admin.PostAsJsonAsync($"/applications/{application!.Id}/versions", new
        {
            version = "1.0.0",
            runtimeMetadata = new { runtimeName = "dotnet9", requiredPorts = Array.Empty<int>() },
        });
        var version = await addVersion.Content.ReadFromJsonAsync<VersionSummaryDto>();

        await admin.PostAsJsonAsync(
            $"/applications/{application.Id}/versions/{version!.Id}/import", new
            {
                schemaVersion = "1.0",
                configurationKeys = new[]
                {
                    new { key = "server.port", targetKind = "application.properties", required = true, secret = false, defaultValue = "9980", valueType = "integer" },
                },
                dependencies = Array.Empty<object>(),
                placeholders = Array.Empty<object>(),
            });

        var scaffold = await admin.GetAsync($"/applications/{application.Id}/versions/{version.Id}/ansible-scaffold");
        Assert.Equal(HttpStatusCode.OK, scaffold.StatusCode);
        var body = await scaffold.Content.ReadFromJsonAsync<AnsibleScaffoldDto>();
        Assert.Equal(application.Slug, body!.ApplicationSlug);
        Assert.Equal("1.0.0", body.Version);
        var template = Assert.Single(body.Files, f => f.RelativePath == $"roles/{application.Slug}/templates/application.properties.j2");
        Assert.Contains("server.port={{ iris_server_port }}", template.Content);
        Assert.Contains(body.Files, f => f.RelativePath == $"playbooks/{application.Slug}.yml");
    }

    [Fact]
    public async Task Generating_an_ansible_scaffold_for_an_unknown_version_returns_not_found()
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

        var response = await admin.GetAsync($"/applications/{application!.Id}/versions/{Guid.NewGuid()}/ansible-scaffold");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    [Fact]
    public async Task Reader_cannot_launch_an_installation_awx_job()
    {
        var response = await Reader().PostAsJsonAsync(
            $"/applications/installations/{Guid.NewGuid()}/awx/launch", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Installation_runs_for_an_unknown_installation_return_not_found()
    {
        var admin = Admin();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await admin.GetAsync($"/applications/installations/{Guid.NewGuid()}/runs")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await admin.GetAsync($"/applications/installations/{Guid.NewGuid()}/runs/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Launching_a_deployment_records_a_failed_run_when_awx_is_not_configured()
    {
        var admin = Admin();
        var name = "svc-" + Guid.NewGuid().ToString("N")[..8];

        var application = await (await admin.PostAsJsonAsync("/applications", new
        {
            name,
            runtimeType = "CSharp",
            repositoryUrl = $"https://git.example/{name}",
            defaultBranch = "main",
        })).Content.ReadFromJsonAsync<ApplicationDto>();

        var version = await (await admin.PostAsJsonAsync($"/applications/{application!.Id}/versions", new
        {
            version = "1.0.0",
            runtimeMetadata = new { runtimeName = "dotnet9", requiredPorts = new[] { 8080 } },
        })).Content.ReadFromJsonAsync<VersionSummaryDto>();

        var server = await (await admin.PostAsJsonAsync("/servers", new
        {
            name = $"{name}-node",
            os = "Linux",
            hostingType = "SelfHosted",
            privateIpAddress = "10.0.5.5",
            environment = "Production",
        })).Content.ReadFromJsonAsync<IdOnlyDto>();

        var customer = await (await admin.PostAsJsonAsync("/customers", new
        {
            key = "cust-" + Guid.NewGuid().ToString("N")[..8],
            name = "Test Customer",
        })).Content.ReadFromJsonAsync<IdOnlyDto>();
        var context = await (await admin.PostAsJsonAsync($"/customers/{customer!.Id}/contexts", new
        {
            name = "Production",
            kind = "Production",
        })).Content.ReadFromJsonAsync<IdOnlyDto>();

        var installation = await (await admin.PostAsJsonAsync($"/applications/{application.Id}/installations", new
        {
            name = $"{name}-prd",
            applicationVersionId = version!.Id,
            serverNodeId = server!.Id,
            customerContextId = context!.Id,
        })).Content.ReadFromJsonAsync<IdOnlyDto>();

        // AWX is not configured in the test host: the launch is rejected...
        var launch = await admin.PostAsJsonAsync(
            $"/applications/installations/{installation!.Id}/awx/launch", new { });
        Assert.Equal(HttpStatusCode.BadRequest, launch.StatusCode);

        // ...but the attempt is recorded as a failed run.
        var runs = await admin.GetFromJsonAsync<List<InstallationRunDto>>(
            $"/applications/installations/{installation.Id}/runs");
        var run = Assert.Single(runs!);
        Assert.Equal("Failed", run.Status);
        Assert.True(run.IsTerminal);

        var detail = await admin.GetAsync($"/applications/installations/{installation.Id}/runs/{run.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    private sealed record IdOnlyDto(Guid Id);

    private sealed record InstallationRunDto(Guid Id, string Status, bool IsTerminal);

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

    private sealed record AnsibleScaffoldFileDto(string RelativePath, string Description, string Content);

    private sealed record AnsibleScaffoldDto(
        string ApplicationSlug, string Version, DateTimeOffset GeneratedAtUtc, List<AnsibleScaffoldFileDto> Files);
}
