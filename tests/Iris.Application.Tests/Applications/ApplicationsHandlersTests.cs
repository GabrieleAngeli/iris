using Iris.Application.Abstractions;
using Iris.Application.Applications;
using Iris.Application.Common;
using Iris.Application.Tests.Fakes;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;
using Iris.Domain.Infrastructure;
using Iris.Domain.Tenancy;

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

    private static CreateApplicationInstallationHandler CreateInstallationHandler(FakeStore store) =>
        new(store.ApplicationRepository, store.ServerRepository, store.DataServiceRepository, store.ApplicationInstallationRepository, store.UnitOfWork);

    private static GetApplicationInstallationAnsiblePlanHandler AnsiblePlanHandler(FakeStore store) =>
        new(store.ApplicationInstallationRepository, store.ApplicationRepository, store.ServerRepository);

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

    [Fact]
    public async Task CreateApplicationInstallation_binds_a_release_unit_profile_to_a_server()
    {
        var store = new FakeStore();
        var server = new ServerNode(
            Guid.CreateVersion7(),
            "engine01",
            "engine01.example",
            ServerOs.Linux,
            ServerHostingType.Cloud,
            null,
            "10.0.0.12",
            ContextKind.Production);
        var database = new DataServiceInstance(
            Guid.CreateVersion7(),
            "prd-pgsql01",
            DataServiceKind.PostgreSql,
            "prd-pgsql01.example",
            5432,
            "augeg4",
            "secret:postgres",
            "16",
            "db.t3.medium",
            1000,
            ContextKind.Production);
        store.WithServer(server);
        store.DataServices.Add(database);

        var app = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "AugeG4 Engine",
            null,
            "Java",
            "https://git.example/augeg4-engine",
            "main",
            null,
            "Nexus",
            "maven-releases",
            "augeg4-engine",
            "com.algorab:augeg4-engine:2026.09.03",
            "https://dev.azure.com/algorab/augeg4/_build?definitionId=42"));
        var version = await AddVersionHandler(store).HandleAsync(new AddApplicationVersionCommand(
            app.Id, "2026.09.03", "refs/tags/2026.09.03", Runtime("java17")));
        await ImportHandler(store).HandleAsync(new ImportConfigurationPackageCommand(
            app.Id,
            version.Id,
            "1.1",
            [new ConfigurationKeyInput(
                "spring.datasource.url",
                "application.properties",
                true,
                true,
                null,
                "PostgreSQL connection string",
                "{database}",
                "domain.augeg4.postgres.connectionString",
                "connectionString",
                null,
                "serviceReference",
                null,
                """{"kind":"serviceReference","serviceKind":"postgresql"}""")],
            [new DependencyInput(
                "postgres",
                "database",
                true,
                "Application database",
                "domain.augeg4.postgres.connectionString")],
            [],
            [],
            [new ApplicationUnitInput(
                "augeg4.engine.master",
                "AugeG4 engine master",
                "service",
                "com.algorab.augeg4.Master",
                "bin/augeg4-engine.jar",
                ["linux-service", "docker"],
                ["master"])],
            [new InstallationProfileInput("master", "Master", true, false, ["spring.datasource.url"])],
            []));

        var savesBeforeInstallation = store.SaveChangesCalls;
        var created = await CreateInstallationHandler(store).HandleAsync(new CreateApplicationInstallationCommand(
            app.Id,
            "augeg4-engine-master-prd",
            version.Id,
            server.Id,
            "Production",
            "augeg4.engine.master",
            "master",
            "primary installation",
            [new ApplicationInstallationBindingInput(
                "domain.augeg4.postgres.connectionString",
                "dataService",
                database.Id,
                null,
                "prd-pgsql01 - PostgreSql",
                null)]));

        Assert.Equal("AugeG4 Engine", created.ApplicationName);
        Assert.Equal("2026.09.03", created.Version);
        Assert.Equal("engine01", created.ServerName);
        Assert.Equal("augeg4.engine.master", created.ApplicationUnitKey);
        Assert.Equal("master", created.InstallationProfileKey);
        var binding = Assert.Single(created.Bindings);
        Assert.Equal("dataService", binding.TargetKind);
        Assert.Equal(database.Id, binding.TargetId);
        Assert.Single(store.ApplicationInstallations);
        Assert.Equal(savesBeforeInstallation + 1, store.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateApplicationInstallation_rejects_unit_not_declared_by_the_manifest()
    {
        var store = new FakeStore();
        var server = new ServerNode(
            Guid.CreateVersion7(),
            "engine01",
            null,
            ServerOs.Linux,
            ServerHostingType.Cloud,
            null,
            null,
            ContextKind.Production);
        store.WithServer(server);

        var app = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "AugeG4 Engine",
            null,
            "Java",
            "https://git.example/augeg4-engine",
            "main",
            null,
            "Nexus",
            "maven-releases",
            "augeg4-engine",
            "com.algorab:augeg4-engine:2026.09.03",
            "https://dev.azure.com/algorab/augeg4/_build?definitionId=42"));
        var version = await AddVersionHandler(store).HandleAsync(new AddApplicationVersionCommand(
            app.Id, "2026.09.03", "refs/tags/2026.09.03", Runtime("java17")));
        await ImportHandler(store).HandleAsync(new ImportConfigurationPackageCommand(
            app.Id,
            version.Id,
            "1.1",
            [],
            [],
            [],
            [],
            [new ApplicationUnitInput("augeg4.engine.master", "Master", "service", null, null, null, null)],
            [],
            []));

        await Assert.ThrowsAsync<ValidationException>(() => CreateInstallationHandler(store).HandleAsync(
            new CreateApplicationInstallationCommand(
                app.Id,
                "bad-installation",
                version.Id,
                server.Id,
                "Production",
                "augeg4.engine.slave",
                null,
                null,
                [])));
    }

    [Fact]
    public async Task GetApplicationInstallationAnsiblePlan_exports_variables_for_jinja_templates()
    {
        var store = new FakeStore();
        var server = new ServerNode(
            Guid.CreateVersion7(),
            "engine01",
            "engine01.example",
            ServerOs.Linux,
            ServerHostingType.Cloud,
            null,
            "10.0.0.12",
            ContextKind.Production);
        var database = new DataServiceInstance(
            Guid.CreateVersion7(),
            "prd-pgsql01",
            DataServiceKind.PostgreSql,
            "prd-pgsql01.example",
            5432,
            "augeg4",
            "secret:postgres",
            "16",
            "db.t3.medium",
            1000,
            ContextKind.Production);
        store.WithServer(server);
        store.DataServices.Add(database);

        var app = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "AugeG4 Engine",
            null,
            "Java",
            "https://git.example/augeg4-engine",
            "main",
            null,
            "Nexus",
            "maven-releases",
            "augeg4-engine",
            "com.algorab:augeg4-engine:2026.09.03",
            "https://dev.azure.com/algorab/augeg4/_build?definitionId=42"));
        var version = await AddVersionHandler(store).HandleAsync(new AddApplicationVersionCommand(
            app.Id, "2026.09.03", "refs/tags/2026.09.03", Runtime("java17")));
        await ImportHandler(store).HandleAsync(new ImportConfigurationPackageCommand(
            app.Id,
            version.Id,
            "1.1",
            [
                new ConfigurationKeyInput(
                    "spring.datasource.url",
                    "application.properties",
                    true,
                    true,
                    null,
                    "PostgreSQL connection string",
                    "{database}",
                    "domain.augeg4.postgres.connectionString",
                    "connectionString",
                    null,
                    "serviceReference",
                    null,
                    """{"kind":"serviceReference","serviceKind":"postgresql"}""",
                    """["master"]"""),
                new ConfigurationKeyInput(
                    "server.port",
                    "application.properties",
                    true,
                    false,
                    "9980",
                    "HTTP port rendered by Ansible",
                    "network:http:port",
                    "domain.augeg4.engine.httpPort",
                    "integer",
                    null,
                    "installationInstance",
                    null,
                    null,
                    """["master"]""")
            ],
            [new DependencyInput(
                "postgres",
                "database",
                true,
                "Application database",
                "domain.augeg4.postgres.connectionString")],
            [],
            [],
            [new ApplicationUnitInput(
                "augeg4.engine.master",
                "Master",
                "service",
                "com.algorab.augeg4.Master",
                "bin/augeg4-engine.jar",
                ["linux-service"],
                ["master"])],
            [new InstallationProfileInput("master", "Master", true, false, ["spring.datasource.url", "server.port"])],
            []));
        var installation = await CreateInstallationHandler(store).HandleAsync(new CreateApplicationInstallationCommand(
            app.Id,
            "augeg4-engine-master-prd",
            version.Id,
            server.Id,
            "Production",
            "augeg4.engine.master",
            "master",
            null,
            [new ApplicationInstallationBindingInput(
                "domain.augeg4.postgres.connectionString",
                "dataService",
                database.Id,
                null,
                "prd-pgsql01 - PostgreSql",
                null)]));

        var plan = await AnsiblePlanHandler(store).HandleAsync(new GetApplicationInstallationAnsiblePlanQuery(installation.Id));

        Assert.Equal("augeg4-engine-master-prd", plan.InstallationName);
        Assert.Equal(["ansible:j2:application.properties"], plan.TemplateTargets);
        Assert.Equal("Nexus", plan.Artifact.Provider);
        Assert.Equal("bin/augeg4-engine.jar", plan.Artifact.Path);
        var association = Assert.Single(plan.Associations);
        Assert.Equal("domain.augeg4.postgres.connectionString", association.PlaceholderKey);
        Assert.Equal("dataService", association.TargetKind);
        Assert.Equal("resolved", association.Status);
        Assert.Contains(plan.Operations, operation =>
            operation.Kind == "configuration.render" &&
            operation.AnsibleModule == "ansible.builtin.template" &&
            operation.Template == "application.properties.j2");
        Assert.Contains(plan.Operations, operation =>
            operation.Kind == "runtime.service" &&
            operation.AnsibleModule == "ansible.builtin.systemd_service" &&
            operation.Template == "systemd/augeg4.engine.master.service.j2");
        Assert.Contains(plan.Operations, operation =>
            operation.Kind == "network.apply" &&
            operation.AnsibleModule == "role:iris.firewall_proxy");
        Assert.Equal(2, plan.Variables.Count);
        var db = plan.Variables.Single(v => v.ConfigurationKey == "spring.datasource.url");
        Assert.Equal("iris_domain_augeg4_postgres_connectionstring", db.Name);
        Assert.Equal("iris:data-service", db.Source);
        Assert.True(db.Secret);
        Assert.Equal("prd-pgsql01 - PostgreSql", db.ValuePreview);
        var port = plan.Variables.Single(v => v.ConfigurationKey == "server.port");
        Assert.Equal("manifest:default", port.Source);
        Assert.Equal("9980", port.ValuePreview);
        Assert.Contains(plan.Warnings, warning => warning.Contains("Ansible", StringComparison.OrdinalIgnoreCase));
    }

    private static ValidateApplicationInstallationHandler ValidateHandler(FakeStore store) =>
        new(store.ApplicationInstallationRepository, store.ApplicationRepository, store.ServerRepository, store.DataServiceRepository);

    private static async Task<(Guid AppId, Guid VersionId)> SeedEngineVersion(
        FakeStore store,
        RuntimeMetadataRequest runtime,
        IReadOnlyList<ConfigurationKeyInput> keys,
        IReadOnlyList<DependencyInput> dependencies,
        IReadOnlyList<PlaceholderInput> placeholders,
        IReadOnlyList<DependencyConstraintInput> constraints)
    {
        var app = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "AugeG4 Engine",
            null,
            "Java",
            "https://git.example/augeg4-engine",
            "main",
            null,
            "Nexus",
            "maven-releases",
            "augeg4-engine",
            "com.algorab:augeg4-engine:1",
            null));
        var version = await AddVersionHandler(store).HandleAsync(new AddApplicationVersionCommand(
            app.Id, "2026.09.03", "refs/tags/2026.09.03", runtime));
        await ImportHandler(store).HandleAsync(new ImportConfigurationPackageCommand(
            app.Id,
            version.Id,
            "1.1",
            keys,
            dependencies,
            placeholders,
            [],
            [new ApplicationUnitInput("augeg4.engine.master", "Master", "service", "Main", "bin/app.jar", ["linux-service"], null)],
            [],
            constraints));
        return (app.Id, version.Id);
    }

    [Fact]
    public async Task ValidateApplicationInstallation_passes_when_release_matches_target()
    {
        var store = new FakeStore();
        var server = new ServerNode(
            Guid.CreateVersion7(),
            "engine01",
            "engine01.example",
            ServerOs.Linux,
            ServerHostingType.Cloud,
            null,
            "10.0.0.12",
            ContextKind.Production);
        server.UpdateCapacity([NodeCapability.ServiceHost], new ResourceProfile(4, 8192, 200), [22, 443]);
        store.WithServer(server);
        var database = new DataServiceInstance(
            Guid.CreateVersion7(),
            "prd-pgsql01",
            DataServiceKind.PostgreSql,
            "prd-pgsql01.example",
            5432,
            "augeg4",
            "secret:postgres",
            "16.2",
            "db.t3.medium",
            1000,
            ContextKind.Production);
        store.DataServices.Add(database);

        var runtime = new RuntimeMetadataRequest(
            "java17", "Linux", 2, 2048, [8080, 8443],
            OsSupport: [new RuntimeOsSupportInfo("linux", "ubuntu", "22.04")],
            MinimumCpuCores: 2,
            MinimumMemoryMb: 2048);
        var (appId, versionId) = await SeedEngineVersion(
            store,
            runtime,
            [new ConfigurationKeyInput(
                "spring.datasource.url", "application.properties", true, true, null,
                "PostgreSQL connection string", null, "domain.augeg4.postgres.connectionString")],
            [new DependencyInput("postgres", "database", true, "Application database", "domain.augeg4.postgres.connectionString")],
            [new PlaceholderInput("domain.augeg4.postgres.connectionString", "database", null, true)],
            [new DependencyConstraintInput("domain.augeg4.postgres.connectionString", "postgresql", ">= 14")]);

        var installation = await CreateInstallationHandler(store).HandleAsync(new CreateApplicationInstallationCommand(
            appId,
            "augeg4-engine-master-prd",
            versionId,
            server.Id,
            "Production",
            "augeg4.engine.master",
            null,
            null,
            [new ApplicationInstallationBindingInput(
                "domain.augeg4.postgres.connectionString", "dataService", database.Id, null, "prd-pgsql01 - PostgreSql", null)]));

        var report = await ValidateHandler(store).HandleAsync(new ValidateApplicationInstallationQuery(installation.Id));

        Assert.True(report.IsValid);
        Assert.Equal(0, report.Errors);
        Assert.DoesNotContain(report.Checks, check => check.Severity == "error");
    }

    [Fact]
    public async Task ValidateApplicationInstallation_collects_blocking_checks()
    {
        var store = new FakeStore();
        var server = new ServerNode(
            Guid.CreateVersion7(),
            "engine01",
            null,
            ServerOs.Linux,
            ServerHostingType.SelfHosted,
            null,
            "10.0.0.20",
            ContextKind.Production);
        server.UpdateCapacity([NodeCapability.Database], new ResourceProfile(2, 1024, 50), [8080]);
        store.WithServer(server);

        var runtime = new RuntimeMetadataRequest(
            "java17", "Linux", 2, 1024, [8080, 8443],
            OsSupport: [new RuntimeOsSupportInfo("windows", null, "2022")],
            MinimumCpuCores: 8,
            MinimumMemoryMb: 4096);
        var (appId, versionId) = await SeedEngineVersion(
            store,
            runtime,
            [new ConfigurationKeyInput(
                "spring.datasource.url", "application.properties", true, true, null,
                "conn", null, "domain.augeg4.postgres.connectionString")],
            [new DependencyInput("postgres", "database", true, "db", "domain.augeg4.postgres.connectionString")],
            [new PlaceholderInput("domain.augeg4.postgres.connectionString", "database", null, true)],
            []);

        var installation = await CreateInstallationHandler(store).HandleAsync(new CreateApplicationInstallationCommand(
            appId,
            "augeg4-engine-master-prd",
            versionId,
            server.Id,
            "Production",
            "augeg4.engine.master",
            null,
            null,
            []));

        var report = await ValidateHandler(store).HandleAsync(new ValidateApplicationInstallationQuery(installation.Id));

        Assert.False(report.IsValid);
        var codes = report.Checks.Select(check => check.Code).ToArray();
        Assert.Contains("placeholder.unbound", codes);
        Assert.Contains("dependency.unbound", codes);
        Assert.Contains("os.incompatible", codes);
        Assert.Contains("capability.missing", codes);
        Assert.Contains("port.collision", codes);
        Assert.Contains("capacity.cpu", codes);
        Assert.Contains("capacity.memory", codes);
        Assert.Contains(report.Checks, check => check.Code == "port.collision" && check.Target == "8080");
        Assert.Equal(report.Errors, report.Checks.Count(check => check.Severity == "error"));
    }

    [Fact]
    public async Task ValidateApplicationInstallation_flags_data_service_version_constraint()
    {
        var store = new FakeStore();
        var server = new ServerNode(
            Guid.CreateVersion7(),
            "engine01",
            null,
            ServerOs.Linux,
            ServerHostingType.Cloud,
            null,
            "10.0.0.12",
            ContextKind.Production);
        server.UpdateCapacity([NodeCapability.ServiceHost], new ResourceProfile(4, 8192, 200), []);
        store.WithServer(server);
        var redis = new DataServiceInstance(
            Guid.CreateVersion7(),
            "prd-redis01",
            DataServiceKind.Redis,
            "prd-redis01.example",
            6379,
            null,
            "secret:redis",
            "6.0.14",
            "cache.t3.small",
            5,
            ContextKind.Production);
        store.DataServices.Add(redis);

        var runtime = new RuntimeMetadataRequest("java17", "Linux", 2, 2048, [8080], MinimumCpuCores: 2, MinimumMemoryMb: 2048);
        var (appId, versionId) = await SeedEngineVersion(
            store,
            runtime,
            [],
            [new DependencyInput("redis", "cache", true, "Cache", "domain.augeg4.redis.endpoint")],
            [new PlaceholderInput("domain.augeg4.redis.endpoint", "cache", null, true)],
            [
                new DependencyConstraintInput("domain.augeg4.redis.endpoint", "redis", ">= 6.2 && < 8"),
                new DependencyConstraintInput("domain.augeg4.mongo.uri", "mongodb", "== 6"),
            ]);

        var installation = await CreateInstallationHandler(store).HandleAsync(new CreateApplicationInstallationCommand(
            appId,
            "augeg4-engine-master-prd",
            versionId,
            server.Id,
            "Production",
            "augeg4.engine.master",
            null,
            null,
            [new ApplicationInstallationBindingInput(
                "domain.augeg4.redis.endpoint", "dataService", redis.Id, null, "prd-redis01 - Redis", null)]));

        var report = await ValidateHandler(store).HandleAsync(new ValidateApplicationInstallationQuery(installation.Id));

        Assert.False(report.IsValid);
        Assert.Contains(report.Checks, check => check.Code == "constraint.version" && check.Severity == "error");
        Assert.DoesNotContain(report.Checks, check => check.Code == "constraint.service-kind");
    }

    [Theory]
    [InlineData(">= 6.2 && < 8", "6.0.14", false)]
    [InlineData(">= 6.2 && < 8", "7.2.4", true)]
    [InlineData("== 6", "6.0.14", true)]
    [InlineData("6.2-8.0", "7.4", true)]
    [InlineData("6.2-8.0", "8.1", false)]
    [InlineData(">= 14", "16.2", true)]
    public void SatisfiesVersion_evaluates_simple_expressions(string expression, string actual, bool expected) =>
        Assert.Equal(expected, ValidateApplicationInstallationHandler.SatisfiesVersion(expression, actual));

    [Theory]
    [InlineData("weird-expr", "1.0")]
    [InlineData(">= 6", "not-a-version")]
    public void SatisfiesVersion_returns_null_when_unparseable(string expression, string actual) =>
        Assert.Null(ValidateApplicationInstallationHandler.SatisfiesVersion(expression, actual));

    private static LaunchApplicationInstallationAwxJobHandler LaunchRunHandler(FakeStore store, FakeAwxClient awx) =>
        new(AnsiblePlanHandler(store), new FakeAnsibleExecutionPackageBuilder(), awx, store.InstallationRunRepository, new FakeClock(Now), store.UnitOfWork);

    private static ListInstallationRunsHandler ListRunsHandler(FakeStore store) =>
        new(store.ApplicationInstallationRepository, store.InstallationRunRepository);

    private static GetInstallationRunHandler GetRunHandler(FakeStore store, FakeAwxClient awx) =>
        new(store.InstallationRunRepository, awx, new FakeClock(Now), store.UnitOfWork);

    private static async Task<Guid> SeedInstallation(FakeStore store)
    {
        var server = new ServerNode(
            Guid.CreateVersion7(), "engine01", "engine01.example",
            ServerOs.Linux, ServerHostingType.Cloud, null, "10.0.0.12", ContextKind.Production);
        server.UpdateCapacity([NodeCapability.ServiceHost], new ResourceProfile(4, 8192, 200), []);
        store.WithServer(server);

        var runtime = new RuntimeMetadataRequest("java17", "Linux", 2, 2048, [8080], MinimumCpuCores: 2, MinimumMemoryMb: 2048);
        var (appId, versionId) = await SeedEngineVersion(
            store,
            runtime,
            [new ConfigurationKeyInput(
                "server.port", "application.properties", true, false, "9980",
                "HTTP port", null, "domain.augeg4.engine.httpPort")],
            [],
            [],
            []);

        var installation = await CreateInstallationHandler(store).HandleAsync(new CreateApplicationInstallationCommand(
            appId, "augeg4-engine-master-prd", versionId, server.Id, "Production",
            "augeg4.engine.master", null, null, []));
        return installation.Id;
    }

    [Fact]
    public async Task LaunchApplicationInstallationAwxJob_records_a_submitted_run()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var awx = new FakeAwxClient
        {
            LaunchResult = new AwxJobLaunchResult(5150, "successful", "https://awx.example/#/jobs/5150", "queued"),
        };

        var response = await LaunchRunHandler(store, awx).HandleAsync(
            new LaunchApplicationInstallationAwxJobCommand(installationId, new ApplicationInstallationAwxLaunchRequest()));

        Assert.NotEqual(Guid.Empty, response.RunId);
        Assert.Equal(5150, response.JobId);
        Assert.Equal(1, awx.LaunchCalls);
        var run = Assert.Single(store.InstallationRuns);
        Assert.Equal(response.RunId, run.Id);
        Assert.Equal(installationId, run.ApplicationInstallationId);
        Assert.Equal(InstallationRunStatus.Succeeded, run.Status);
        Assert.Equal("5150", run.ExternalJobId);
        Assert.NotNull(run.CompletedAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(run.SubmittedVariablesJson));
    }

    [Fact]
    public async Task LaunchApplicationInstallationAwxJob_records_a_failed_run_and_rethrows()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var awx = new FakeAwxClient { ThrowOnLaunch = true, LaunchFailureMessage = "AWX endpoint, token and job template id are required." };

        await Assert.ThrowsAsync<ValidationException>(() => LaunchRunHandler(store, awx).HandleAsync(
            new LaunchApplicationInstallationAwxJobCommand(installationId, new ApplicationInstallationAwxLaunchRequest())));

        var run = Assert.Single(store.InstallationRuns);
        Assert.Equal(InstallationRunStatus.Failed, run.Status);
        Assert.Contains("job template id", run.Message);
        Assert.NotNull(run.CompletedAtUtc);
    }

    [Fact]
    public async Task ListInstallationRuns_returns_history_and_404s_for_unknown_installation()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var awx = new FakeAwxClient();

        await LaunchRunHandler(store, awx).HandleAsync(
            new LaunchApplicationInstallationAwxJobCommand(installationId, new ApplicationInstallationAwxLaunchRequest()));
        await LaunchRunHandler(store, awx).HandleAsync(
            new LaunchApplicationInstallationAwxJobCommand(installationId, new ApplicationInstallationAwxLaunchRequest()));

        var history = await ListRunsHandler(store).HandleAsync(new ListInstallationRunsQuery(installationId));
        Assert.Equal(2, history.Count);
        Assert.All(history, run => Assert.Equal(installationId, run.InstallationId));

        await Assert.ThrowsAsync<NotFoundException>(() => ListRunsHandler(store)
            .HandleAsync(new ListInstallationRunsQuery(Guid.NewGuid())));
    }

    [Fact]
    public async Task GetInstallationRun_refreshes_status_from_awx_until_terminal()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var awx = new FakeAwxClient
        {
            LaunchResult = new AwxJobLaunchResult(77, "pending", "https://awx.example/#/jobs/77", null),
        };

        var launched = await LaunchRunHandler(store, awx).HandleAsync(
            new LaunchApplicationInstallationAwxJobCommand(installationId, new ApplicationInstallationAwxLaunchRequest()));

        awx.JobStatus = new AwxJobStatusResult("successful", Finished: true, Succeeded: true, "https://awx.example/#/jobs/77", null);
        var refreshed = await GetRunHandler(store, awx).HandleAsync(new GetInstallationRunQuery(installationId, launched.RunId));

        Assert.Equal("Succeeded", refreshed.Status);
        Assert.True(refreshed.IsTerminal);
        Assert.Equal(1, awx.StatusCalls);

        // Once terminal, the read no longer polls AWX.
        await GetRunHandler(store, awx).HandleAsync(new GetInstallationRunQuery(installationId, launched.RunId));
        Assert.Equal(1, awx.StatusCalls);

        await Assert.ThrowsAsync<NotFoundException>(() => GetRunHandler(store, awx)
            .HandleAsync(new GetInstallationRunQuery(Guid.NewGuid(), launched.RunId)));
    }
}
