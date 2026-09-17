using Iris.Application.Abstractions;
using Iris.Application.Applications;
using Iris.Application.Common;
using Iris.Application.Tests.Fakes;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;
using Iris.Domain.Infrastructure;
using Iris.Domain.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Iris.Application.Tests.Applications;

public sealed class ApplicationsHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static CreateApplicationHandler CreateHandler(FakeStore store) =>
        new(store.ApplicationRepository, store.SecretStore, store.UnitOfWork);

    private static AddApplicationVersionHandler AddVersionHandler(FakeStore store) =>
        new(store.ApplicationRepository, store.UnitOfWork);

    private static UpdateApplicationHandler UpdateHandler(FakeStore store) =>
        new(store.ApplicationRepository, store.SecretStore, store.UnitOfWork);

    private static ImportConfigurationPackageHandler ImportHandler(FakeStore store) =>
        new(store.ApplicationRepository, new FakeClock(Now), store.UnitOfWork);

    private static CreateApplicationInstallationHandler CreateInstallationHandler(FakeStore store) =>
        new(store.ApplicationRepository, store.ServerRepository, store.DataServiceRepository, store.CustomerRepository, store.ApplicationInstallationRepository, store.UnitOfWork);

    private static GetApplicationInstallationAnsiblePlanHandler AnsiblePlanHandler(FakeStore store) =>
        new(store.ApplicationInstallationRepository, store.ApplicationRepository, store.ServerRepository, store.CustomerRepository);

    private static RuntimeMetadataRequest Runtime(string name = "dotnet9", string? os = "Linux") =>
        new(name, os, 2, 1024, [8080, 8443]);

    /// <summary>Seeds a customer with one context and returns the context id, for tests that need a real <c>CustomerContextId</c>.</summary>
    private static Guid SeedCustomerContext(FakeStore store, ContextKind kind = ContextKind.Production, string contextName = "Production")
    {
        var customer = new Customer(Guid.CreateVersion7(), $"cust-{Guid.NewGuid():N}"[..12], "Test Customer");
        var context = customer.AddContext(Guid.CreateVersion7(), contextName, kind);
        store.WithCustomer(customer);
        return context.Id;
    }

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
        var contextId = SeedCustomerContext(store);
        var created = await CreateInstallationHandler(store).HandleAsync(new CreateApplicationInstallationCommand(
            app.Id,
            "augeg4-engine-master-prd",
            version.Id,
            server.Id,
            contextId,
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
                Guid.NewGuid(),
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
            SeedCustomerContext(store),
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

    private static GenerateApplicationAnsibleScaffoldHandler AnsibleScaffoldHandler(FakeStore store) =>
        new(store.ApplicationRepository, new FakeClock(Now));

    [Fact]
    public async Task GenerateApplicationAnsibleScaffold_throws_not_found_for_missing_application()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<NotFoundException>(() => AnsibleScaffoldHandler(store).HandleAsync(
            new GenerateApplicationAnsibleScaffoldQuery(Guid.NewGuid(), Guid.NewGuid())));
    }

    [Fact]
    public async Task GenerateApplicationAnsibleScaffold_throws_not_found_for_missing_version()
    {
        var store = new FakeStore();
        var app = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "AugeG4 Engine", null, "Java", "https://git.example/augeg4-engine", "main", null));

        await Assert.ThrowsAsync<NotFoundException>(() => AnsibleScaffoldHandler(store).HandleAsync(
            new GenerateApplicationAnsibleScaffoldQuery(app.Id, Guid.NewGuid())));
    }

    [Fact]
    public async Task GenerateApplicationAnsibleScaffold_returns_role_and_playbook_skeleton_for_the_selected_version()
    {
        var store = new FakeStore();
        var app = await CreateHandler(store).HandleAsync(new CreateApplicationCommand(
            "AugeG4 Engine", null, "Java", "https://git.example/augeg4-engine", "main", null));
        var version = await AddVersionHandler(store).HandleAsync(new AddApplicationVersionCommand(
            app.Id, "4.0.0", "refs/tags/4.0.0", Runtime("java17")));
        await ImportHandler(store).HandleAsync(new ImportConfigurationPackageCommand(
            app.Id,
            version.Id,
            "1.1",
            [new ConfigurationKeyInput(
                "server.port", "application.properties", true, false, "9980", null, null, null, "integer", null, null, null, null, null)],
            [],
            [],
            [],
            [],
            [],
            []));

        var scaffold = await AnsibleScaffoldHandler(store).HandleAsync(new GenerateApplicationAnsibleScaffoldQuery(app.Id, version.Id));

        Assert.Equal("augeg4-engine", scaffold.ApplicationSlug);
        Assert.Equal("4.0.0", scaffold.Version);
        Assert.Equal(Now, scaffold.GeneratedAtUtc);
        Assert.Contains(scaffold.Files, f => f.RelativePath == "roles/augeg4-engine/templates/application.properties.j2" &&
            f.Content.Contains("server.port={{ iris_server_port }}", StringComparison.Ordinal));
        Assert.Contains(scaffold.Files, f => f.RelativePath == "playbooks/augeg4-engine.yml");
    }

    private static ValidateApplicationInstallationHandler ValidateHandler(FakeStore store) =>
        new(store.ApplicationInstallationRepository, store.ApplicationRepository, store.ServerRepository, store.DataServiceRepository, store.CustomerRepository);

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
            SeedCustomerContext(store),
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
            SeedCustomerContext(store),
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
            SeedCustomerContext(store),
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
        new(store.InstallationRunRepository, new InstallationRunRefresher(awx, new FakeClock(Now)), store.UnitOfWork);

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
            appId, "augeg4-engine-master-prd", versionId, server.Id, SeedCustomerContext(store),
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

    private static InstallationRunRefresher Refresher(FakeAwxClient awx) => new(awx, new FakeClock(Now));

    [Fact]
    public async Task InstallationRunRefresher_no_ops_when_the_run_is_already_terminal()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var awx = new FakeAwxClient { LaunchResult = new AwxJobLaunchResult(1, "successful", null, null) };
        var launched = await LaunchRunHandler(store, awx).HandleAsync(
            new LaunchApplicationInstallationAwxJobCommand(installationId, new ApplicationInstallationAwxLaunchRequest()));
        var run = await store.InstallationRunRepository.GetForUpdateAsync(launched.RunId);

        await Refresher(awx).RefreshAsync(run!);

        Assert.Equal(0, awx.StatusCalls);
    }

    [Fact]
    public async Task InstallationRunRefresher_no_ops_when_there_is_no_external_job_id_yet()
    {
        var run = new InstallationRun(Guid.CreateVersion7(), Guid.CreateVersion7(), InstallationRunKind.AwxJob, null);
        var awx = new FakeAwxClient();

        await Refresher(awx).RefreshAsync(run);

        Assert.Equal(0, awx.StatusCalls);
    }

    [Fact]
    public async Task InstallationRunRefresher_updates_status_without_capturing_outcome_while_still_running()
    {
        var run = new InstallationRun(Guid.CreateVersion7(), Guid.CreateVersion7(), InstallationRunKind.AwxJob, null);
        run.MarkSubmitted("42", null, InstallationRunStatus.Pending, null, Now);
        var awx = new FakeAwxClient { JobStatus = new AwxJobStatusResult("running", false, false, null, "queued") };

        await Refresher(awx).RefreshAsync(run);

        Assert.Equal(InstallationRunStatus.Running, run.Status);
        Assert.False(run.IsTerminal);
        Assert.Null(run.ElapsedSeconds);
        Assert.Equal(0, awx.OutputCalls);
    }

    [Fact]
    public async Task InstallationRunRefresher_captures_elapsed_and_output_when_newly_terminal()
    {
        var run = new InstallationRun(Guid.CreateVersion7(), Guid.CreateVersion7(), InstallationRunKind.AwxJob, null);
        run.MarkSubmitted("42", null, InstallationRunStatus.Running, null, Now);
        var awx = new FakeAwxClient
        {
            JobStatus = new AwxJobStatusResult("successful", true, true, null, null, 12.5),
            JobOutput = "PLAY [deploy] ***\nok: [host]",
        };

        await Refresher(awx).RefreshAsync(run);

        Assert.Equal(InstallationRunStatus.Succeeded, run.Status);
        Assert.True(run.IsTerminal);
        Assert.Equal(12.5, run.ElapsedSeconds);
        Assert.Equal("PLAY [deploy] ***\nok: [host]", run.Output);
        Assert.Equal(1, awx.OutputCalls);
    }

    [Fact]
    public async Task InstallationRunRefresher_keeps_the_last_known_status_when_awx_is_unreachable()
    {
        var run = new InstallationRun(Guid.CreateVersion7(), Guid.CreateVersion7(), InstallationRunKind.AwxJob, null);
        run.MarkSubmitted("42", null, InstallationRunStatus.Running, null, Now);
        var awx = new FakeAwxClient { JobStatus = null };

        await Refresher(awx).RefreshAsync(run);

        Assert.Equal(InstallationRunStatus.Running, run.Status);
        Assert.Null(run.ElapsedSeconds);
    }

    [Fact]
    public async Task InstallationRunRefresher_still_captures_elapsed_when_the_output_fetch_fails()
    {
        var run = new InstallationRun(Guid.CreateVersion7(), Guid.CreateVersion7(), InstallationRunKind.AwxJob, null);
        run.MarkSubmitted("42", null, InstallationRunStatus.Running, null, Now);
        var awx = new FakeAwxClient
        {
            JobStatus = new AwxJobStatusResult("failed", true, false, null, "playbook error", 3.2),
            ThrowOnOutput = true,
        };

        await Refresher(awx).RefreshAsync(run);

        Assert.Equal(InstallationRunStatus.Failed, run.Status);
        Assert.Equal(3.2, run.ElapsedSeconds);
        Assert.Null(run.Output);
    }

    private static PollActiveInstallationRunsHandler PollHandler(FakeStore store, FakeAwxClient awx) =>
        new(store.InstallationRunRepository, Refresher(awx), store.UnitOfWork, NullLogger<PollActiveInstallationRunsHandler>.Instance);

    [Fact]
    public async Task PollActiveInstallationRuns_refreshes_every_active_run_and_returns_the_count()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var awx = new FakeAwxClient { LaunchResult = new AwxJobLaunchResult(1, "running", null, null) };
        await LaunchRunHandler(store, awx).HandleAsync(
            new LaunchApplicationInstallationAwxJobCommand(installationId, new ApplicationInstallationAwxLaunchRequest()));
        await LaunchRunHandler(store, awx).HandleAsync(
            new LaunchApplicationInstallationAwxJobCommand(installationId, new ApplicationInstallationAwxLaunchRequest()));
        awx.JobStatus = new AwxJobStatusResult("successful", true, true, null, null, 5.0);

        var refreshedCount = await PollHandler(store, awx).HandleAsync();

        Assert.Equal(2, refreshedCount);
        Assert.All(store.InstallationRuns, run => Assert.Equal(InstallationRunStatus.Succeeded, run.Status));
    }

    [Fact]
    public async Task PollActiveInstallationRuns_does_not_touch_runs_that_are_already_terminal()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var awx = new FakeAwxClient { LaunchResult = new AwxJobLaunchResult(1, "successful", null, null) };
        await LaunchRunHandler(store, awx).HandleAsync(
            new LaunchApplicationInstallationAwxJobCommand(installationId, new ApplicationInstallationAwxLaunchRequest()));

        var refreshedCount = await PollHandler(store, awx).HandleAsync();

        Assert.Equal(0, refreshedCount);
        Assert.Equal(0, awx.StatusCalls);
    }

    private static PrepareApplicationInstallationActionHandler PrepareActionHandler(FakeStore store) =>
        new(store.ApplicationInstallationRepository, ValidateHandler(store), AnsiblePlanHandler(store), store.PreparedActionRepository, store.UnitOfWork);

    private static ExecutePreparedActionHandler ExecuteActionHandler(FakeStore store, FakeAwxClient awx) =>
        new(store.PreparedActionRepository, ValidateHandler(store), LaunchRunHandler(store, awx), store.InstallationRunRepository, new FakeClock(Now), store.UnitOfWork);

    private static CancelPreparedActionHandler CancelActionHandler(FakeStore store) =>
        new(store.PreparedActionRepository, new FakeClock(Now), store.UnitOfWork);

    private static ListPreparedActionsHandler ListActionsHandler(FakeStore store) =>
        new(store.PreparedActionRepository, store.ApplicationInstallationRepository, store.ApplicationRepository, store.ServerRepository, store.CustomerRepository, store.InstallationRunRepository);

    private static GetPreparedActionHandler GetActionHandler(FakeStore store) =>
        new(store.PreparedActionRepository, store.InstallationRunRepository);

    [Fact]
    public async Task PrepareApplicationInstallationAction_freezes_a_reviewable_plan_and_validation_snapshot()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);

        var prepared = await PrepareActionHandler(store).HandleAsync(
            new PrepareApplicationInstallationActionCommand(installationId, new ApplicationInstallationAwxLaunchRequest(JobTemplateId: 42)));

        Assert.Equal("Prepared", prepared.Status);
        Assert.Equal(installationId, prepared.InstallationId);
        Assert.True(prepared.Validation.IsValid);
        Assert.Equal(42, prepared.RequestedLaunchOptions.JobTemplateId);
        Assert.Null(prepared.InstallationRunId);
        Assert.Null(prepared.Run);
        var stored = Assert.Single(store.PreparedActions);
        Assert.Equal(PreparedActionStatus.Prepared, stored.Status);
        Assert.Contains(installationId.ToString(), stored.PlanSnapshotJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareApplicationInstallationAction_throws_not_found_for_missing_installation() =>
        await Assert.ThrowsAsync<NotFoundException>(() => PrepareActionHandler(new FakeStore()).HandleAsync(
            new PrepareApplicationInstallationActionCommand(Guid.NewGuid(), null)));

    [Fact]
    public async Task ExecutePreparedAction_launches_the_awx_job_and_marks_the_action_executed()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var prepared = await PrepareActionHandler(store).HandleAsync(
            new PrepareApplicationInstallationActionCommand(installationId, null));
        var awx = new FakeAwxClient
        {
            LaunchResult = new AwxJobLaunchResult(999, "successful", "https://awx.example/#/jobs/999", null),
        };

        var executed = await ExecuteActionHandler(store, awx).HandleAsync(new ExecutePreparedActionCommand(prepared.Id));

        Assert.Equal("Executed", executed.Status);
        Assert.NotNull(executed.InstallationRunId);
        Assert.NotNull(executed.Run);
        Assert.Equal("Succeeded", executed.Run!.Status);
        var stored = Assert.Single(store.PreparedActions);
        Assert.Equal(PreparedActionStatus.Executed, stored.Status);
        Assert.Equal(1, awx.LaunchCalls);
    }

    [Fact]
    public async Task ExecutePreparedAction_throws_when_already_executed()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var prepared = await PrepareActionHandler(store).HandleAsync(
            new PrepareApplicationInstallationActionCommand(installationId, null));
        var awx = new FakeAwxClient();
        await ExecuteActionHandler(store, awx).HandleAsync(new ExecutePreparedActionCommand(prepared.Id));

        await Assert.ThrowsAsync<ValidationException>(() => ExecuteActionHandler(store, awx)
            .HandleAsync(new ExecutePreparedActionCommand(prepared.Id)));
    }

    [Fact]
    public async Task ExecutePreparedAction_blocks_when_fresh_validation_now_has_errors()
    {
        var store = new FakeStore();
        var server = new ServerNode(
            Guid.CreateVersion7(), "engine01", null,
            ServerOs.Linux, ServerHostingType.SelfHosted, null, "10.0.0.20", ContextKind.Production);
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
            appId, "augeg4-engine-master-prd", versionId, server.Id, SeedCustomerContext(store),
            "augeg4.engine.master", null, null, []));

        var prepared = await PrepareActionHandler(store).HandleAsync(
            new PrepareApplicationInstallationActionCommand(installation.Id, null));
        Assert.False(prepared.Validation.IsValid); // Prepare still succeeds — seeing the risk is the point.

        var awx = new FakeAwxClient();
        await Assert.ThrowsAsync<ValidationException>(() => ExecuteActionHandler(store, awx)
            .HandleAsync(new ExecutePreparedActionCommand(prepared.Id)));
        Assert.Equal(0, awx.LaunchCalls);
        Assert.Equal(PreparedActionStatus.Prepared, Assert.Single(store.PreparedActions).Status);
    }

    [Fact]
    public async Task CancelPreparedAction_transitions_to_canceled()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var prepared = await PrepareActionHandler(store).HandleAsync(
            new PrepareApplicationInstallationActionCommand(installationId, null));

        var canceled = await CancelActionHandler(store).HandleAsync(
            new CancelPreparedActionCommand(prepared.Id, "not needed anymore"));

        Assert.Equal("Canceled", canceled.Status);
        Assert.Equal("not needed anymore", canceled.CancelReason);
    }

    [Fact]
    public async Task CancelPreparedAction_throws_when_already_executed()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var prepared = await PrepareActionHandler(store).HandleAsync(
            new PrepareApplicationInstallationActionCommand(installationId, null));
        await ExecuteActionHandler(store, new FakeAwxClient()).HandleAsync(new ExecutePreparedActionCommand(prepared.Id));

        await Assert.ThrowsAsync<ValidationException>(() => CancelActionHandler(store)
            .HandleAsync(new CancelPreparedActionCommand(prepared.Id, null)));
    }

    [Fact]
    public async Task ListPreparedActions_reports_the_linked_run_status_as_the_effective_status_once_executed()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var prepared = await PrepareActionHandler(store).HandleAsync(
            new PrepareApplicationInstallationActionCommand(installationId, null));
        var awx = new FakeAwxClient
        {
            LaunchResult = new AwxJobLaunchResult(1, "running", null, null),
        };
        await ExecuteActionHandler(store, awx).HandleAsync(new ExecutePreparedActionCommand(prepared.Id));

        var summaries = await ListActionsHandler(store).HandleAsync(new ListPreparedActionsQuery());

        var summary = Assert.Single(summaries);
        Assert.Equal("Running", summary.EffectiveStatus);
        Assert.Equal(installationId, summary.InstallationId);
        Assert.Equal("augeg4-engine", summary.ApplicationSlug);
    }

    [Fact]
    public async Task ListPreparedActions_filters_by_application_and_status()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var prepared = await PrepareActionHandler(store).HandleAsync(
            new PrepareApplicationInstallationActionCommand(installationId, null));
        var otherApplicationId = Guid.NewGuid();

        var byWrongApplication = await ListActionsHandler(store).HandleAsync(
            new ListPreparedActionsQuery(ApplicationId: otherApplicationId));
        Assert.Empty(byWrongApplication);

        var byStatus = await ListActionsHandler(store).HandleAsync(new ListPreparedActionsQuery(Status: "Prepared"));
        Assert.Single(byStatus);

        var byWrongStatus = await ListActionsHandler(store).HandleAsync(new ListPreparedActionsQuery(Status: "Canceled"));
        Assert.Empty(byWrongStatus);
        _ = prepared;
    }

    [Fact]
    public async Task GetPreparedAction_returns_the_frozen_snapshot_and_throws_for_unknown_id()
    {
        var store = new FakeStore();
        var installationId = await SeedInstallation(store);
        var prepared = await PrepareActionHandler(store).HandleAsync(
            new PrepareApplicationInstallationActionCommand(installationId, null));

        var fetched = await GetActionHandler(store).HandleAsync(new GetPreparedActionQuery(prepared.Id));

        Assert.Equal(prepared.Id, fetched.Id);
        Assert.Equal(installationId, fetched.Plan.InstallationId);

        await Assert.ThrowsAsync<NotFoundException>(() => GetActionHandler(store)
            .HandleAsync(new GetPreparedActionQuery(Guid.NewGuid())));
    }
}
