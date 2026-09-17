using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Application.Settings;
using Iris.Application.Tests.Fakes;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Tests.Settings;

public sealed class IntegrationSettingsHandlersTests
{
    private static SaveOpenBaoIntegrationSettingsHandler OpenBaoHandler(FakeStore store) =>
        new(store.IntegrationSettingsRepository, store.SecretStore, store.UnitOfWork, store.IntegrationSettingsReloader);

    private static SaveAwxIntegrationSettingsHandler AwxHandler(FakeStore store) =>
        new(store.IntegrationSettingsRepository, store.SecretStore, store.UnitOfWork, store.IntegrationSettingsReloader);

    private static SaveAnsibleIntegrationSettingsHandler AnsibleHandler(FakeStore store) =>
        new(store.IntegrationSettingsRepository, store.UnitOfWork, store.IntegrationSettingsReloader);

    private static SaveAzureDevOpsIntegrationSettingsHandler AzureDevOpsHandler(FakeStore store) =>
        new(store.IntegrationSettingsRepository, store.SecretStore, store.UnitOfWork, store.IntegrationSettingsReloader);

    private static SaveNexusIntegrationSettingsHandler NexusHandler(FakeStore store) =>
        new(store.IntegrationSettingsRepository, store.SecretStore, store.UnitOfWork, store.IntegrationSettingsReloader);

    private static SaveOpsHostIntegrationSettingsHandler OpsHostHandler(FakeStore store) =>
        new(store.IntegrationSettingsRepository, store.SecretStore, store.UnitOfWork, store.IntegrationSettingsReloader);

    private static GetSystemSettingsHandler SystemSettingsHandler(
        FakeStore store, IEnumerable<IIntegrationConnector>? connectors = null,
        IIntegrationHealthMonitor? healthMonitor = null, ISecretStorePromotion? secretStorePromotion = null) =>
        new(store.MailProviderSettingsRepository, store.IntegrationSettingsRepository,
            connectors ?? [],
            new FakeCurrentUser(Guid.CreateVersion7()),
            new FakeUserProvisioningService(new Iris.Domain.Access.User(Guid.CreateVersion7(), "ext-1", "admin@iris.local", "Admin")),
            new FakeFallbackSecretVault(),
            healthMonitor ?? new FakeIntegrationHealthMonitor(),
            secretStorePromotion ?? new FakeSecretStorePromotion());

    /// <summary>Stands in for a real <c>IIntegrationConnector</c> (e.g. <c>OpenBaoConnector</c>)
    /// reporting whatever its live configuration currently says.</summary>
    private sealed class FakeConnector(string key, string name, string? endpoint) : IIntegrationConnector
    {
        public string Key => key;
        public string Name => name;
        public string? Endpoint => endpoint;

        public Task<IntegrationConnectorStatus> GetStatusAsync(bool probe = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IntegrationConnectorStatus(
                Key, Name, string.IsNullOrWhiteSpace(endpoint) ? "Not configured" : "Configured", endpoint));
    }

    [Fact]
    public async Task SaveOpenBao_creates_the_row_and_stores_the_token()
    {
        var store = new FakeStore();

        var result = await OpenBaoHandler(store).HandleAsync(
            new SaveOpenBaoIntegrationSettingsCommand("https://openbao.example.com", "root-token", "secret", true));

        Assert.False(result.RestartRequired);
        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal("https://openbao.example.com", settings.OpenBaoEndpoint);
        Assert.Equal("root-token", store.SecretsByReference[settings.OpenBaoTokenSecretReference!]);
    }

    [Fact]
    public async Task SaveOpenBao_with_a_blank_token_keeps_the_previously_stored_reference()
    {
        var store = new FakeStore();
        await OpenBaoHandler(store).HandleAsync(
            new SaveOpenBaoIntegrationSettingsCommand("https://openbao.example.com", "root-token", "secret", true));
        var firstReference = store.IntegrationSettings.Single().OpenBaoTokenSecretReference;

        await OpenBaoHandler(store).HandleAsync(
            new SaveOpenBaoIntegrationSettingsCommand("https://openbao.example.com", null, "kv-v2", true));

        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal("kv-v2", settings.OpenBaoMountPath);
        Assert.Equal(firstReference, settings.OpenBaoTokenSecretReference);
        Assert.Equal("root-token", store.SecretsByReference[settings.OpenBaoTokenSecretReference!]);
    }

    [Fact]
    public async Task SaveOpenBao_rejects_a_blank_endpoint()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<ValidationException>(() =>
            OpenBaoHandler(store).HandleAsync(new SaveOpenBaoIntegrationSettingsCommand("", "token", "secret", true)));

        Assert.Empty(store.IntegrationSettings);
    }

    [Fact]
    public async Task SaveAwx_creates_the_row_and_stores_the_token()
    {
        var store = new FakeStore();

        var result = await AwxHandler(store).HandleAsync(
            new SaveAwxIntegrationSettingsCommand("https://awx.example.com", "awx-token", 7));

        Assert.False(result.RestartRequired);
        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal("https://awx.example.com", settings.AwxEndpoint);
        Assert.Equal(7, settings.AwxJobTemplateId);
        Assert.Equal("awx-token", store.SecretsByReference[settings.AwxTokenSecretReference!]);
    }

    [Fact]
    public async Task SaveAwx_with_a_blank_token_keeps_the_previously_stored_reference()
    {
        var store = new FakeStore();
        await AwxHandler(store).HandleAsync(new SaveAwxIntegrationSettingsCommand("https://awx.example.com", "awx-token", 7));
        var firstReference = store.IntegrationSettings.Single().AwxTokenSecretReference;

        await AwxHandler(store).HandleAsync(new SaveAwxIntegrationSettingsCommand("https://awx.example.com", null, 9));

        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal(9, settings.AwxJobTemplateId);
        Assert.Equal(firstReference, settings.AwxTokenSecretReference);
    }

    [Fact]
    public async Task SaveAwx_with_a_null_job_template_id_keeps_the_stored_one()
    {
        var store = new FakeStore();
        await AwxHandler(store).HandleAsync(new SaveAwxIntegrationSettingsCommand("https://awx.example.com", "awx-token", 7));

        // Re-save from a partial Configure form that only changed the endpoint.
        await AwxHandler(store).HandleAsync(new SaveAwxIntegrationSettingsCommand("https://awx.example.com/", null, null));

        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal(7, settings.AwxJobTemplateId);
    }

    [Fact]
    public async Task SaveAwx_with_a_null_facts_job_template_id_keeps_the_stored_one()
    {
        var store = new FakeStore();
        await AwxHandler(store).HandleAsync(
            new SaveAwxIntegrationSettingsCommand("https://awx.example.com", "awx-token", 7, FactsJobTemplateId: 42));

        // Re-save from a partial Configure form that only changed the deploy job template id.
        await AwxHandler(store).HandleAsync(
            new SaveAwxIntegrationSettingsCommand("https://awx.example.com", null, 8, FactsJobTemplateId: null));

        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal(8, settings.AwxJobTemplateId);
        Assert.Equal(42, settings.AwxFactsJobTemplateId);
    }

    [Fact]
    public async Task SaveAzureDevOps_with_blank_repo_fields_keeps_the_previously_stored_values()
    {
        var store = new FakeStore();
        await AzureDevOpsHandler(store).HandleAsync(new SaveAzureDevOpsIntegrationSettingsCommand(
            "https://dev.azure.com/algorab-devops", "pat", "Refactoring_ops_flow", "awx", "master",
            "automation/manifests/awx_context_blueprints.yml"));

        // Re-save from a partial Configure form that only changed the endpoint.
        await AzureDevOpsHandler(store).HandleAsync(
            new SaveAzureDevOpsIntegrationSettingsCommand("https://dev.azure.com/algorab-devops/", null));

        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal("Refactoring_ops_flow", settings.AzureDevOpsProject);
        Assert.Equal("awx", settings.AzureDevOpsRepository);
        Assert.Equal("master", settings.AzureDevOpsBranch);
        Assert.Equal("automation/manifests/awx_context_blueprints.yml", settings.AzureDevOpsManifestPath);
    }

    [Fact]
    public async Task SaveOpsHost_creates_the_row_and_stores_the_secret()
    {
        var store = new FakeStore();

        var result = await OpsHostHandler(store).HandleAsync(
            new SaveOpsHostIntegrationSettingsCommand("opsserver.internal", 22, "ops", "SshKey", "-----BEGIN KEY-----"));

        Assert.False(result.RestartRequired);
        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal("opsserver.internal", settings.OpsHostEndpoint);
        Assert.Equal("ops", settings.OpsHostUsername);
        Assert.Equal(ServerCredentialAuthMethod.SshKey, settings.OpsHostAuthMethod);
        Assert.Equal("-----BEGIN KEY-----", store.SecretsByReference[settings.OpsHostSecretReference!]);
    }

    [Fact]
    public async Task SaveOpsHost_with_a_blank_secret_keeps_the_previously_stored_reference()
    {
        var store = new FakeStore();
        await OpsHostHandler(store).HandleAsync(
            new SaveOpsHostIntegrationSettingsCommand("opsserver.internal", 22, "ops", "SshKey", "key-v1"));
        var firstReference = store.IntegrationSettings.Single().OpsHostSecretReference;

        await OpsHostHandler(store).HandleAsync(
            new SaveOpsHostIntegrationSettingsCommand("opsserver.internal", 22, "ops", "SshKey", null));

        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal(firstReference, settings.OpsHostSecretReference);
    }

    [Fact]
    public async Task SaveOpsHost_rejects_an_unknown_auth_method()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<ValidationException>(() =>
            OpsHostHandler(store).HandleAsync(
                new SaveOpsHostIntegrationSettingsCommand("opsserver.internal", 22, "ops", "Kerberos", "secret")));
    }

    [Fact]
    public async Task SaveAwx_rejects_a_blank_endpoint()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<ValidationException>(() =>
            AwxHandler(store).HandleAsync(new SaveAwxIntegrationSettingsCommand("", "token", 1)));

        Assert.Empty(store.IntegrationSettings);
    }

    [Fact]
    public async Task SaveAnsible_creates_the_row()
    {
        var store = new FakeStore();

        var result = await AnsibleHandler(store).HandleAsync(
            new SaveAnsibleIntegrationSettingsCommand("https://ansible.example.com", "deploy.yml", "hosts.ini"));

        Assert.False(result.RestartRequired);
        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal("https://ansible.example.com", settings.AnsibleEndpoint);
        Assert.Equal("deploy.yml", settings.AnsiblePlaybook);
        Assert.Equal("hosts.ini", settings.AnsibleInventory);
    }

    [Fact]
    public async Task SaveAnsible_rejects_a_blank_endpoint_or_playbook()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<ValidationException>(() =>
            AnsibleHandler(store).HandleAsync(new SaveAnsibleIntegrationSettingsCommand("", "deploy.yml", null)));
        await Assert.ThrowsAsync<ValidationException>(() =>
            AnsibleHandler(store).HandleAsync(new SaveAnsibleIntegrationSettingsCommand("https://ansible.example.com", "", null)));

        Assert.Empty(store.IntegrationSettings);
    }

    [Fact]
    public async Task SaveAzureDevOps_creates_the_row_and_stores_the_token()
    {
        var store = new FakeStore();

        var result = await AzureDevOpsHandler(store).HandleAsync(
            new SaveAzureDevOpsIntegrationSettingsCommand("https://dev.azure.com/contoso", "pat-token"));

        Assert.False(result.RestartRequired);
        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal("https://dev.azure.com/contoso", settings.AzureDevOpsEndpoint);
        Assert.Equal("pat-token", store.SecretsByReference[settings.AzureDevOpsTokenSecretReference!]);
    }

    [Fact]
    public async Task SaveAzureDevOps_with_a_blank_token_keeps_the_previously_stored_reference()
    {
        var store = new FakeStore();
        await AzureDevOpsHandler(store).HandleAsync(
            new SaveAzureDevOpsIntegrationSettingsCommand("https://dev.azure.com/contoso", "pat-token"));
        var firstReference = store.IntegrationSettings.Single().AzureDevOpsTokenSecretReference;

        await AzureDevOpsHandler(store).HandleAsync(
            new SaveAzureDevOpsIntegrationSettingsCommand("https://dev.azure.com/contoso-renamed", null));

        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal("https://dev.azure.com/contoso-renamed", settings.AzureDevOpsEndpoint);
        Assert.Equal(firstReference, settings.AzureDevOpsTokenSecretReference);
    }

    [Fact]
    public async Task SaveAzureDevOps_rejects_a_blank_endpoint()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<ValidationException>(() =>
            AzureDevOpsHandler(store).HandleAsync(new SaveAzureDevOpsIntegrationSettingsCommand("", "token")));

        Assert.Empty(store.IntegrationSettings);
    }

    [Fact]
    public async Task SaveNexus_creates_the_row_and_stores_the_token()
    {
        var store = new FakeStore();

        var result = await NexusHandler(store).HandleAsync(
            new SaveNexusIntegrationSettingsCommand("https://nexus.example.com", "nexus-token"));

        Assert.False(result.RestartRequired);
        var settings = Assert.Single(store.IntegrationSettings);
        Assert.Equal("https://nexus.example.com", settings.NexusEndpoint);
        Assert.Equal("nexus-token", store.SecretsByReference[settings.NexusTokenSecretReference!]);
    }

    [Fact]
    public async Task SaveNexus_rejects_a_blank_endpoint()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<ValidationException>(() =>
            NexusHandler(store).HandleAsync(new SaveNexusIntegrationSettingsCommand("", "token")));

        Assert.Empty(store.IntegrationSettings);
    }

    [Fact]
    public async Task GetSystemSettings_reports_azure_devops_and_nexus_as_not_configured_when_their_connector_says_so()
    {
        // Regression test for a real bug reported by the user (2026-09-08): "in system mi trovo
        // Azure DevOps e Nexus configured ma non lo sono". These two used to read
        // Iris:Integrations:AzureDevOps/Nexus:Endpoint from appsettings and show "Configured"
        // whenever that config had *any* non-blank value — but appsettings.Development.json
        // ships example placeholder values there that no admin ever actually set. They now have
        // real connectors (AzureDevOpsConnector/NexusConnector) wired the same way as
        // openbao/awx/ansible — this exercises that same pipeline, standing in for the real
        // connector with a fake reporting "no endpoint configured", same as it would for a fresh
        // install.
        var store = new FakeStore();

        var result = await SystemSettingsHandler(
                store,
                connectors:
                [
                    new FakeConnector("azure-devops", "Azure DevOps", endpoint: null),
                    new FakeConnector("nexus", "Nexus Repository", endpoint: null),
                ])
            .HandleAsync(new GetSystemSettingsQuery(true));

        var azureDevOps = Assert.Single(result.Integrations, i => i.Key == "azure-devops");
        Assert.Equal("Not configured", azureDevOps.Status);

        var nexus = Assert.Single(result.Integrations, i => i.Key == "nexus");
        Assert.Equal("Not configured", nexus.Status);
    }

    [Fact]
    public async Task Saving_awx_settings_reconfigures_the_live_connector_immediately()
    {
        // The centerpiece of the 2026-09-16 fix: a save used to only ever update the DB row —
        // the live connector kept using whatever was true at process startup until a restart.
        // Every Save*IntegrationSettingsHandler now calls IIntegrationSettingsReloader right
        // after persisting, so the change is live without one. This is exercised for real
        // (a real AwxOptions singleton actually re-read and mutated) in
        // Iris.Infrastructure.Tests; here we just confirm the handler calls it at all.
        var store = new FakeStore();

        await AwxHandler(store).HandleAsync(new SaveAwxIntegrationSettingsCommand("https://awx.example.com", "awx-token", 7));

        Assert.Equal(1, store.IntegrationSettingsReloader.ReloadCalls);
    }

    [Fact]
    public async Task GetSystemSettings_overlays_the_real_probed_status_from_the_health_monitor()
    {
        // Requested by the user (2026-09-08): "un servizio che controlla i servizi connessi se
        // sono raggiungibili e configurati correttamente" — this is what surfaces that
        // background-probed result. The live connector itself only ever reports "Configured"
        // (config presence, probe:false, cheap); the health monitor's last real (probe:true)
        // check is what tells the operator whether it actually works.
        var store = new FakeStore();
        var checkedAt = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        var monitor = new FakeIntegrationHealthMonitor().Seed("openbao", "Unreachable", "Connection refused.", checkedAt);

        var result = await SystemSettingsHandler(
                store,
                [new FakeConnector("openbao", "OpenBao", endpoint: "https://openbao.example.com")],
                monitor)
            .HandleAsync(new GetSystemSettingsQuery(true));

        var openBao = Assert.Single(result.Integrations, i => i.Key == "openbao");
        Assert.Equal("Unreachable", openBao.Status);
        Assert.Equal("Connection refused.", openBao.Message);
        Assert.Equal(checkedAt, openBao.CheckedAtUtc);
    }

    [Fact]
    public async Task GetSystemSettings_surfaces_fallback_secret_status_from_the_vault_for_the_current_user()
    {
        // Regression test for a real bug: this used to resolve the caller via
        // ICurrentUser.UserId (the "iris:uid" claim) directly, which a real dev-header+password
        // authenticated request left unset — see FallbackSecretVaultApiTests's end-to-end test
        // and GetSystemSettingsHandler's remarks. Locks in the fix (resolve via
        // IUserProvisioningService.EnsureProvisionedAsync instead) at the unit level too.
        var store = new FakeStore();
        var user = new Iris.Domain.Access.User(Guid.CreateVersion7(), "ext-1", "admin@iris.local", "Admin");
        var vault = new FakeFallbackSecretVault { Status = new(true, 2, 0) };

        var handler = new GetSystemSettingsHandler(
            store.MailProviderSettingsRepository, store.IntegrationSettingsRepository,
            [],
            new FakeCurrentUser(Guid.CreateVersion7()),
            new FakeUserProvisioningService(user),
            vault,
            new FakeIntegrationHealthMonitor(),
            new FakeSecretStorePromotion());

        var result = await handler.HandleAsync(new GetSystemSettingsQuery(true));

        Assert.NotNull(result.FallbackSecrets);
        Assert.True(result.FallbackSecrets!.HasPendingWork);
        Assert.Equal(2, result.FallbackSecrets.PendingPersistCount);
    }

    [Fact]
    public async Task GetSystemSettings_reports_openbao_as_the_active_secret_store_once_promoted()
    {
        var store = new FakeStore();
        var promotion = new FakeSecretStorePromotion { IsOpenBaoActive = true };

        var result = await SystemSettingsHandler(
                store,
                [new FakeConnector("openbao", "OpenBao", endpoint: "https://openbao.example.com")],
                secretStorePromotion: promotion)
            .HandleAsync(new GetSystemSettingsQuery(true));

        var openBao = Assert.Single(result.Integrations, i => i.Key == "openbao");
        Assert.True(openBao.IsSecretStoreActive);
    }

    [Fact]
    public async Task GetSystemSettings_prefills_the_awx_blueprint_row_with_the_same_azure_devops_settings()
    {
        // "AWX blueprint" has no Configure dialog of its own — clicking Configure on it opens the
        // same Azure DevOps dialog (they save through the same PUT /system/integrations/azure-devops),
        // so it needs the same pre-fill fields, under a dedicated AzureDevOpsEndpoint (its own
        // Endpoint is a "project/repo@branch:path" summary, not the organization URL).
        var store = new FakeStore();
        await AzureDevOpsHandler(store).HandleAsync(new SaveAzureDevOpsIntegrationSettingsCommand(
            "https://dev.azure.com/algorab-devops", "pat", "Refactoring_ops_flow", "awx", "master",
            "automation/manifests/awx_context_blueprints.yml"));

        var result = await SystemSettingsHandler(
                store,
                [new FakeConnector("awx-blueprint", "AWX blueprint", endpoint: "Refactoring_ops_flow/awx@master:automation/manifests/awx_context_blueprints.yml")])
            .HandleAsync(new GetSystemSettingsQuery(true));

        var blueprint = Assert.Single(result.Integrations, i => i.Key == "awx-blueprint");
        Assert.Equal("https://dev.azure.com/algorab-devops", blueprint.AzureDevOpsEndpoint);
        Assert.Equal("Refactoring_ops_flow", blueprint.AzureDevOpsProject);
        Assert.Equal("awx", blueprint.AzureDevOpsRepository);
        Assert.Equal("master", blueprint.AzureDevOpsBranch);
        Assert.Equal("automation/manifests/awx_context_blueprints.yml", blueprint.AzureDevOpsManifestPath);
        // Its own Endpoint is untouched — still the human-readable summary, not the org URL.
        Assert.Equal("Refactoring_ops_flow/awx@master:automation/manifests/awx_context_blueprints.yml", blueprint.Endpoint);
    }

    [Fact]
    public async Task GetSystemSettings_hides_mail_settings_from_callers_who_cannot_manage_the_system()
    {
        var store = new FakeStore();

        var result = await SystemSettingsHandler(store).HandleAsync(new GetSystemSettingsQuery(false));

        Assert.False(result.CanManageSystem);
        Assert.Null(result.Mail);
    }

    [Theory]
    [InlineData("Reachable", true)]
    [InlineData("Unreachable", false)]
    [InlineData("Not configured", false)]
    public async Task TestOpenBao_maps_reachable_to_succeeded(string status, bool expectedSucceeded)
    {
        var tester = new FakeIntegrationConnectionTester { NextStatus = new IntegrationConnectorStatus("openbao", "OpenBao", status, "endpoint", "a message") };
        var handler = new TestOpenBaoIntegrationSettingsHandler(tester);

        var result = await handler.HandleAsync(new SaveOpenBaoIntegrationSettingsCommand("https://openbao.example", "token", "secret", true));

        Assert.Equal(expectedSucceeded, result.Succeeded);
        Assert.Equal(status, result.Status);
        Assert.Equal("a message", result.Message);
    }

    [Theory]
    [InlineData("Reachable", true)]
    [InlineData("Configured", false)]
    public async Task TestAwx_only_reachable_counts_as_succeeded(string status, bool expectedSucceeded)
    {
        var tester = new FakeIntegrationConnectionTester { NextStatus = new IntegrationConnectorStatus("awx", "AWX", status, "endpoint") };
        var handler = new TestAwxIntegrationSettingsHandler(tester);

        var result = await handler.HandleAsync(new SaveAwxIntegrationSettingsCommand("https://awx.example", "token", 1));

        Assert.Equal(expectedSucceeded, result.Succeeded);
    }

    [Fact]
    public async Task TestAzureDevOps_reports_reachable_as_succeeded()
    {
        var tester = new FakeIntegrationConnectionTester
        {
            NextStatus = new IntegrationConnectorStatus("azure-devops", "Azure DevOps", "Reachable", "endpoint", "Organization reachable; blueprint manifest found."),
        };
        var handler = new TestAzureDevOpsIntegrationSettingsHandler(tester);

        var result = await handler.HandleAsync(new SaveAzureDevOpsIntegrationSettingsCommand("https://dev.azure.com/org", "pat"));

        Assert.True(result.Succeeded);
        Assert.Equal("Organization reachable; blueprint manifest found.", result.Message);
    }

    [Fact]
    public async Task TestNexus_reports_unreachable_as_not_succeeded()
    {
        var tester = new FakeIntegrationConnectionTester
        {
            NextStatus = new IntegrationConnectorStatus("nexus", "Nexus", "Unreachable", "endpoint", "Connection refused"),
        };
        var handler = new TestNexusIntegrationSettingsHandler(tester);

        var result = await handler.HandleAsync(new SaveNexusIntegrationSettingsCommand("http://nexus.example", null));

        Assert.False(result.Succeeded);
        Assert.Equal("Connection refused", result.Message);
    }

    [Fact]
    public async Task TestOpsHost_reports_reachable_as_succeeded()
    {
        var tester = new FakeIntegrationConnectionTester
        {
            NextStatus = new IntegrationConnectorStatus("ops-host", "Ops host", "Reachable", "endpoint"),
        };
        var handler = new TestOpsHostIntegrationSettingsHandler(tester);

        var result = await handler.HandleAsync(new SaveOpsHostIntegrationSettingsCommand("opshost.example", 22, "ops", "SshKey", "secret"));

        Assert.True(result.Succeeded);
    }
}
