using Iris.Application.Applications;
using Iris.Application.Common;
using Iris.Application.Tests.Fakes;
using Iris.Contracts.Applications;

namespace Iris.Application.Tests.Applications;

public sealed class ApplicationsHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static CreateApplicationHandler CreateHandler(FakeStore store) =>
        new(store.ApplicationRepository, store.UnitOfWork);

    private static AddApplicationVersionHandler AddVersionHandler(FakeStore store) =>
        new(store.ApplicationRepository, store.UnitOfWork);

    private static UpdateApplicationHandler UpdateHandler(FakeStore store) =>
        new(store.ApplicationRepository, store.UnitOfWork);

    private static ImportConfigurationPackageHandler ImportHandler(FakeStore store) =>
        new(store.ApplicationRepository, new FakeClock(Now), store.UnitOfWork);

    private static RuntimeMetadataRequest Runtime(string name = "dotnet9", string? os = "Linux") =>
        new(name, os, 2, 1024, [8080, 8443]);

    [Fact]
    public async Task CreateApplication_auto_generates_slug_from_name()
    {
        var store = new FakeStore();

        var created = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Iris Notification Service",
            null,
            "CSharp",
            "https://git.example/iris-notify",
            "main",
            null,
            "Nexus",
            "iris/releases",
            "iris-notify.zip",
            "releases/iris-notify.zip",
            "https://dev.azure.com/org/iris/_build?definitionId=42"));

        Assert.Equal("iris-notification-service", created.Slug);
        Assert.Equal("Nexus", created.ArtifactProvider);
        Assert.Equal("iris/releases", created.ArtifactFeed);
        Assert.Equal("iris-notify.zip", created.ArtifactName);
        Assert.Equal("releases/iris-notify.zip", created.ArtifactPath);
        Assert.Equal("https://dev.azure.com/org/iris/_build?definitionId=42", created.BuildPipelineUrl);
        Assert.Single(store.Applications);
    }

    [Fact]
    public async Task CreateApplication_rejects_duplicate_slug()
    {
        var store = new FakeStore();
        await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Notify", "notify", "CSharp", "https://git.example/notify", "main", null));

        await Assert.ThrowsAsync<ConflictException>(() => CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Notify Two", "notify", "CSharp", "https://git.example/notify2", "main", null)));
    }

    [Fact]
    public async Task CreateApplication_rejects_unknown_runtime_type()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Notify", null, "Cobol", "https://git.example/notify", "main", null)));
    }

    [Fact]
    public async Task UpdateApplication_updates_inventory_fields_but_keeps_slug()
    {
        var store = new FakeStore();
        var created = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Notify", null, "CSharp", "https://git.example/notify", "main", null));

        var updated = await UpdateHandler(store).HandleAsync(new UpdateApplicationCommand(
            created.Id,
            "Notification Gateway",
            "Docker",
            "https://git.example/notification-gateway",
            "release/main",
            "Inbound notification edge service",
            false,
            "AzureDevOps",
            "drop",
            "notification-gateway.zip",
            "drop/notification-gateway.zip",
            "https://dev.azure.com/org/project/_build?definitionId=7"));

        Assert.Equal("Notification Gateway", updated.Name);
        Assert.Equal("notify", updated.Slug);
        Assert.Equal("Docker", updated.RuntimeType);
        Assert.Equal("https://git.example/notification-gateway", updated.RepositoryUrl);
        Assert.Equal("release/main", updated.DefaultBranch);
        Assert.Equal("Inbound notification edge service", updated.Description);
        Assert.Equal("AzureDevOps", updated.ArtifactProvider);
        Assert.Equal("drop", updated.ArtifactFeed);
        Assert.Equal("notification-gateway.zip", updated.ArtifactName);
        Assert.Equal("drop/notification-gateway.zip", updated.ArtifactPath);
        Assert.Equal("https://dev.azure.com/org/project/_build?definitionId=7", updated.BuildPipelineUrl);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task UpdateApplication_rejects_unknown_application_or_runtime()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<NotFoundException>(() => UpdateHandler(store).HandleAsync(new UpdateApplicationCommand(
            Guid.NewGuid(), "Notify", "CSharp", "https://git.example/notify", "main", null, true)));

        var created = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Notify", null, "CSharp", "https://git.example/notify", "main", null));

        await Assert.ThrowsAsync<ValidationException>(() => UpdateHandler(store).HandleAsync(new UpdateApplicationCommand(
            created.Id, "Notify", "Cobol", "https://git.example/notify", "main", null, true)));
    }

    [Fact]
    public async Task AddApplicationVersion_rejects_duplicate_version()
    {
        var store = new FakeStore();
        var app = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Notify", null, "CSharp", "https://git.example/notify", "main", null));

        await AddVersionHandler(store).HandleAsync(new AddApplicationVersionCommand(
            app.Id, "1.0.0", null, Runtime()));

        await Assert.ThrowsAsync<ConflictException>(() => AddVersionHandler(store).HandleAsync(
            new AddApplicationVersionCommand(app.Id, "1.0.0", null, Runtime())));
    }

    [Fact]
    public async Task AddApplicationVersion_rejects_unknown_application_or_os()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<NotFoundException>(() => AddVersionHandler(store).HandleAsync(
            new AddApplicationVersionCommand(Guid.NewGuid(), "1.0.0", null, Runtime())));

        var app = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Notify", null, "CSharp", "https://git.example/notify", "main", null));

        await Assert.ThrowsAsync<ValidationException>(() => AddVersionHandler(store).HandleAsync(
            new AddApplicationVersionCommand(app.Id, "1.0.0", null, Runtime(os: "MacOS"))));
    }

    [Fact]
    public async Task ImportConfigurationPackage_replaces_the_previous_snapshot_and_keeps_the_raw_package()
    {
        var store = new FakeStore();
        var app = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Notify", null, "CSharp", "https://git.example/notify", "main", null));
        var version = await AddVersionHandler(store).HandleAsync(new AddApplicationVersionCommand(
            app.Id, "1.0.0", null, Runtime()));

        var firstImport = await ImportHandler(store).HandleAsync(new ImportConfigurationPackageCommand(
            app.Id, version.Id, "1.0",
            [new ConfigurationKeyInput(
                "ConnectionStrings:Main",
                "appsettings.json",
                true,
                true,
                null,
                null,
                null,
                "domain.db.main.connectionString",
                "connectionString",
                null,
                "serviceReference",
                null,
                """{"kind":"serviceReference","serviceKind":"postgresql"}""",
                """["master"]""",
                null,
                null)],
            [new DependencyInput("postgres", "database", true, null, "domain.db.main", "orders-api", "domain.db.main.connectionString")],
            [new PlaceholderInput("domain.db.main.connectionString", "database", null, true)],
            ["Unresolved placeholder: domain.cache.redis"],
            [new ApplicationUnitInput(
                "notify.worker",
                "Notify worker",
                "worker",
                "Notify.Worker.Program",
                "drop/notify-worker.dll",
                ["linux-service", "docker"],
                ["master"])],
            [new InstallationProfileInput("master", "Master", true, false, ["ConnectionStrings:Main"])],
            [new DependencyConstraintInput(
                "domain.db.main.connectionString",
                "postgresql",
                ">= 16",
                """{"version":{"minInclusive":"16"}}""")]));

        Assert.Single(firstImport.ConfigurationKeys);
        Assert.Equal("connectionString", firstImport.ConfigurationKeys.Single().ValueType);
        Assert.Equal("serviceReference", firstImport.ConfigurationKeys.Single().Scope);
        Assert.Contains("postgresql", firstImport.ConfigurationKeys.Single().ResolutionJson);
        var dependency = Assert.Single(firstImport.Dependencies);
        Assert.Equal("orders-api", dependency.ProviderApplicationSlug);
        Assert.Equal("domain.db.main.connectionString", dependency.ProviderPlaceholderKey);
        Assert.Single(firstImport.Placeholders);
        var unit = Assert.Single(firstImport.ApplicationUnits);
        Assert.Equal("notify.worker", unit.Key);
        Assert.Contains("docker", unit.ExecutionTargets);
        var profile = Assert.Single(firstImport.InstallationProfiles);
        Assert.Equal("master", profile.Key);
        Assert.Contains("ConnectionStrings:Main", profile.ConfigurationKeys);
        var constraint = Assert.Single(firstImport.DependencyConstraints);
        Assert.Equal("postgresql", constraint.ServiceKind);
        Assert.Equal(">= 16", constraint.VersionExpression);
        Assert.Single(firstImport.ImportWarnings);
        Assert.Equal(Now, firstImport.LastImportedAtUtc);
        Assert.Equal("1.0", firstImport.LastImportSchemaVersion);

        var secondImport = await ImportHandler(store).HandleAsync(new ImportConfigurationPackageCommand(
            app.Id, version.Id, "1.1",
            [],
            [],
            [],
            []));

        Assert.Empty(secondImport.ConfigurationKeys);
        Assert.Empty(secondImport.Dependencies);
        Assert.Empty(secondImport.Placeholders);
        Assert.Empty(secondImport.ApplicationUnits);
        Assert.Empty(secondImport.InstallationProfiles);
        Assert.Empty(secondImport.DependencyConstraints);
        Assert.Empty(secondImport.ImportWarnings);
        Assert.Equal("1.1", secondImport.LastImportSchemaVersion);
    }

    [Fact]
    public async Task ImportConfigurationPackage_rejects_unknown_version()
    {
        var store = new FakeStore();
        var app = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Notify", null, "CSharp", "https://git.example/notify", "main", null));

        await Assert.ThrowsAsync<NotFoundException>(() => ImportHandler(store).HandleAsync(
            new ImportConfigurationPackageCommand(app.Id, Guid.NewGuid(), "1.0", [], [], [], [])));
    }

    [Fact]
    public async Task ListApplications_orders_by_name()
    {
        var store = new FakeStore();
        await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Zeta Service", null, "Node", "https://git.example/zeta", "main", null));
        await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Alpha Service", null, "Java", "https://git.example/alpha", "main", null));

        var result = await new ListApplicationsHandler(store.ApplicationRepository).HandleAsync(new ListApplicationsQuery());

        Assert.Equal(["Alpha Service", "Zeta Service"], result.Select(a => a.Name));
    }

    [Fact]
    public async Task GetApplicationVersionDetail_returns_the_full_configuration_knowledge()
    {
        var store = new FakeStore();
        var app = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "Notify", null, "CSharp", "https://git.example/notify", "main", null));
        var version = await AddVersionHandler(store).HandleAsync(new AddApplicationVersionCommand(
            app.Id, "1.0.0", null, Runtime()));

        await ImportHandler(store).HandleAsync(new ImportConfigurationPackageCommand(
            app.Id, version.Id, "1.0",
            [new ConfigurationKeyInput("Key", "env", true, false, null, null, null, null)],
            [], [], []));

        var detail = await new GetApplicationVersionDetailHandler(store.ApplicationRepository)
            .HandleAsync(new GetApplicationVersionDetailQuery(app.Id, version.Id));

        Assert.Single(detail.ConfigurationKeys);
        Assert.Equal("1.0.0", detail.Version);

        await Assert.ThrowsAsync<NotFoundException>(() => new GetApplicationVersionDetailHandler(store.ApplicationRepository)
            .HandleAsync(new GetApplicationVersionDetailQuery(app.Id, Guid.NewGuid())));
    }
}
