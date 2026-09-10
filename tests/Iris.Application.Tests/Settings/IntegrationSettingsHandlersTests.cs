using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Application.Settings;
using Iris.Application.Tests.Fakes;

namespace Iris.Application.Tests.Settings;

public sealed class IntegrationSettingsHandlersTests
{
    private static SaveOpenBaoIntegrationSettingsHandler OpenBaoHandler(FakeStore store) =>
        new(store.IntegrationSettingsRepository, store.SecretStore, store.UnitOfWork);

    private static SaveAwxIntegrationSettingsHandler AwxHandler(FakeStore store) =>
        new(store.IntegrationSettingsRepository, store.SecretStore, store.UnitOfWork);

    private static SaveAnsibleIntegrationSettingsHandler AnsibleHandler(FakeStore store) =>
        new(store.IntegrationSettingsRepository, store.UnitOfWork);

    private static SaveAzureDevOpsIntegrationSettingsHandler AzureDevOpsHandler(FakeStore store) =>
        new(store.IntegrationSettingsRepository, store.SecretStore, store.UnitOfWork);

    private static SaveNexusIntegrationSettingsHandler NexusHandler(FakeStore store) =>
        new(store.IntegrationSettingsRepository, store.SecretStore, store.UnitOfWork);

    private static GetSystemSettingsHandler SystemSettingsHandler(
        FakeStore store, ActiveIntegrationSnapshot? snapshot = null, IEnumerable<IIntegrationConnector>? connectors = null,
        IIntegrationHealthMonitor? healthMonitor = null) =>
        new(store.MailProviderSettingsRepository, store.IntegrationSettingsRepository,
            snapshot ?? new ActiveIntegrationSnapshot(null, null, null),
            connectors ?? [],
            new FakeCurrentUser(Guid.CreateVersion7()),
            new FakeUserProvisioningService(new Iris.Domain.Access.User(Guid.CreateVersion7(), "ext-1", "admin@iris.local", "Admin")),
            new FakeFallbackSecretVault(),
            healthMonitor ?? new FakeIntegrationHealthMonitor());

    /// <summary>Stands in for <c>OpenBaoConnector</c> — reports whatever the active (pre-restart)
    /// config locked in, exactly like the real connector does, so <c>GetSystemSettingsHandler</c>'s
    /// pending-restart override can be exercised without touching Infrastructure.</summary>
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

        Assert.True(result.RestartRequired);
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

        Assert.True(result.RestartRequired);
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

        Assert.True(result.RestartRequired);
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

        Assert.True(result.RestartRequired);
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

        Assert.True(result.RestartRequired);
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
    public async Task GetSystemSettings_surfaces_pending_restart_for_a_saved_azure_devops_endpoint()
    {
        var store = new FakeStore();
        await AzureDevOpsHandler(store).HandleAsync(
            new SaveAzureDevOpsIntegrationSettingsCommand("https://dev.azure.com/contoso", "pat-token"));

        var result = await SystemSettingsHandler(
                store,
                new ActiveIntegrationSnapshot(null, null, null),
                [new FakeConnector("azure-devops", "Azure DevOps", endpoint: null)])
            .HandleAsync(new GetSystemSettingsQuery(true));

        var azureDevOps = Assert.Single(result.Integrations, i => i.Key == "azure-devops");
        Assert.Equal("Pending restart", azureDevOps.Status);
        Assert.Equal("https://dev.azure.com/contoso", azureDevOps.Endpoint);
    }

    [Fact]
    public async Task GetSystemSettings_reports_RestartRequired_false_when_nothing_is_persisted_and_nothing_active()
    {
        var store = new FakeStore();

        var result = await SystemSettingsHandler(store).HandleAsync(new GetSystemSettingsQuery(true));

        Assert.False(result.RestartRequired);
    }

    [Fact]
    public async Task GetSystemSettings_reports_RestartRequired_true_when_a_saved_endpoint_differs_from_what_is_active()
    {
        var store = new FakeStore();
        await OpenBaoHandler(store).HandleAsync(
            new SaveOpenBaoIntegrationSettingsCommand("https://openbao.example.com", "root-token", "secret", true));

        // The running process locked in "no OpenBao endpoint" at startup — a save afterward
        // means it's out of date until a restart.
        var result = await SystemSettingsHandler(store, new ActiveIntegrationSnapshot(null, null, null))
            .HandleAsync(new GetSystemSettingsQuery(true));

        Assert.True(result.RestartRequired);
    }

    [Fact]
    public async Task GetSystemSettings_reports_RestartRequired_false_when_the_active_snapshot_already_matches()
    {
        var store = new FakeStore();
        await OpenBaoHandler(store).HandleAsync(
            new SaveOpenBaoIntegrationSettingsCommand("https://openbao.example.com", "root-token", "secret", true));

        var result = await SystemSettingsHandler(
                store, new ActiveIntegrationSnapshot("https://openbao.example.com", null, null))
            .HandleAsync(new GetSystemSettingsQuery(true));

        Assert.False(result.RestartRequired);
    }

    [Fact]
    public async Task GetSystemSettings_ignores_a_never_persisted_group_even_if_the_active_config_has_a_default()
    {
        // Regression test for a real bug found via manual end-to-end verification
        // (2026-09-08): dev appsettings ships non-null default endpoints for AWX/Ansible that
        // were never saved through PUT/provision. The active snapshot reflects those config
        // defaults directly (non-null), while nothing was ever persisted for those two groups
        // (null) — that must not count as "a change pending a restart".
        var store = new FakeStore();
        await OpenBaoHandler(store).HandleAsync(
            new SaveOpenBaoIntegrationSettingsCommand("https://openbao.example.com", "root-token", "secret", true));

        var result = await SystemSettingsHandler(
                store,
                new ActiveIntegrationSnapshot(
                    OpenBaoEndpoint: "https://openbao.example.com", // matches what was just persisted
                    AwxEndpoint: "http://localhost:8043",           // config default, never persisted
                    AnsibleEndpoint: "http://localhost:8043"))      // config default, never persisted
            .HandleAsync(new GetSystemSettingsQuery(true));

        Assert.False(result.RestartRequired);
    }

    [Fact]
    public async Task GetSystemSettings_surfaces_the_persisted_endpoint_and_a_pending_restart_status_when_not_yet_active()
    {
        // Regression test for a real usability complaint (2026-09-08): "use existing OpenBao" in
        // the wizard saved the endpoint/token correctly, but the System settings row kept showing
        // the old (unconfigured) live connector status — a real save looked exactly like a no-op.
        var store = new FakeStore();
        await OpenBaoHandler(store).HandleAsync(
            new SaveOpenBaoIntegrationSettingsCommand("https://openbao.example.com", "root-token", "secret", true));

        var result = await SystemSettingsHandler(
                store,
                new ActiveIntegrationSnapshot(null, null, null),
                [new FakeConnector("openbao", "OpenBao", endpoint: null)])
            .HandleAsync(new GetSystemSettingsQuery(true));

        var openBao = Assert.Single(result.Integrations, i => i.Key == "openbao");
        Assert.Equal("Pending restart", openBao.Status);
        Assert.Equal("https://openbao.example.com", openBao.Endpoint);
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
                new ActiveIntegrationSnapshot(null, null, null),
                [new FakeConnector("openbao", "OpenBao", endpoint: "https://openbao.example.com")],
                monitor)
            .HandleAsync(new GetSystemSettingsQuery(true));

        var openBao = Assert.Single(result.Integrations, i => i.Key == "openbao");
        Assert.Equal("Unreachable", openBao.Status);
        Assert.Equal("Connection refused.", openBao.Message);
        Assert.Equal(checkedAt, openBao.CheckedAtUtc);
    }

    [Fact]
    public async Task GetSystemSettings_prefers_pending_restart_over_a_stale_health_check_of_the_old_config()
    {
        // The health monitor only ever probes the currently-ACTIVE (pre-restart) connector — once
        // a new endpoint is saved but not yet active, that probe result describes the OLD config,
        // not the one the operator just saved. "Pending restart" must win.
        var store = new FakeStore();
        await OpenBaoHandler(store).HandleAsync(
            new SaveOpenBaoIntegrationSettingsCommand("https://openbao.example.com", "root-token", "secret", true));
        var monitor = new FakeIntegrationHealthMonitor()
            .Seed("openbao", "Unreachable", "stale check of the old endpoint", DateTimeOffset.UtcNow);

        var result = await SystemSettingsHandler(
                store,
                new ActiveIntegrationSnapshot(null, null, null),
                [new FakeConnector("openbao", "OpenBao", endpoint: null)],
                monitor)
            .HandleAsync(new GetSystemSettingsQuery(true));

        var openBao = Assert.Single(result.Integrations, i => i.Key == "openbao");
        Assert.Equal("Pending restart", openBao.Status);
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
            new ActiveIntegrationSnapshot(null, null, null), [],
            new FakeCurrentUser(Guid.CreateVersion7()),
            new FakeUserProvisioningService(user),
            vault,
            new FakeIntegrationHealthMonitor());

        var result = await handler.HandleAsync(new GetSystemSettingsQuery(true));

        Assert.NotNull(result.FallbackSecrets);
        Assert.True(result.FallbackSecrets!.HasPendingWork);
        Assert.Equal(2, result.FallbackSecrets.PendingPersistCount);
    }

    [Fact]
    public async Task GetSystemSettings_leaves_the_live_status_alone_once_it_matches_what_was_persisted()
    {
        var store = new FakeStore();
        await OpenBaoHandler(store).HandleAsync(
            new SaveOpenBaoIntegrationSettingsCommand("https://openbao.example.com", "root-token", "secret", true));

        var result = await SystemSettingsHandler(
                store,
                new ActiveIntegrationSnapshot("https://openbao.example.com", null, null),
                [new FakeConnector("openbao", "OpenBao", endpoint: "https://openbao.example.com")])
            .HandleAsync(new GetSystemSettingsQuery(true));

        var openBao = Assert.Single(result.Integrations, i => i.Key == "openbao");
        Assert.Equal("Configured", openBao.Status);
    }

    [Fact]
    public async Task GetSystemSettings_hides_mail_settings_from_callers_who_cannot_manage_the_system()
    {
        var store = new FakeStore();

        var result = await SystemSettingsHandler(store).HandleAsync(new GetSystemSettingsQuery(false));

        Assert.False(result.CanManageSystem);
        Assert.Null(result.Mail);
    }
}
