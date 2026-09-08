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

    private static GetSystemSettingsHandler SystemSettingsHandler(FakeStore store, ActiveIntegrationSnapshot? snapshot = null) =>
        new(store.MailProviderSettingsRepository, store.IntegrationSettingsRepository,
            snapshot ?? new ActiveIntegrationSnapshot(null, null, null),
            []);

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
    public async Task GetSystemSettings_reports_RestartRequired_false_when_nothing_is_persisted_and_nothing_active()
    {
        var store = new FakeStore();

        var result = await SystemSettingsHandler(store).HandleAsync(new GetSystemSettingsQuery(true, null, null));

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
            .HandleAsync(new GetSystemSettingsQuery(true, null, null));

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
            .HandleAsync(new GetSystemSettingsQuery(true, null, null));

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
            .HandleAsync(new GetSystemSettingsQuery(true, null, null));

        Assert.False(result.RestartRequired);
    }

    [Fact]
    public async Task GetSystemSettings_hides_mail_settings_from_callers_who_cannot_manage_the_system()
    {
        var store = new FakeStore();

        var result = await SystemSettingsHandler(store).HandleAsync(new GetSystemSettingsQuery(false, null, null));

        Assert.False(result.CanManageSystem);
        Assert.Null(result.Mail);
    }
}
