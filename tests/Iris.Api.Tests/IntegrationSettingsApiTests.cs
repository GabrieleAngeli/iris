using System.Net;
using System.Net.Http.Json;
using Iris.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Api.Tests;

public sealed class IntegrationSettingsApiTests(IrisApiFactory factory) : IClassFixture<IrisApiFactory>
{
    private FakeContainerRuntime ContainerRuntime =>
        (FakeContainerRuntime)factory.Services.GetRequiredService<IContainerRuntime>();

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
    public async Task A_manual_probe_records_into_the_health_monitor_so_settings_reflects_it_afterward()
    {
        // Requested by the user (2026-09-08): "un servizio che controlla i servizi connessi se
        // sono raggiungibili" — a manual Test click is a real probe too, and should count.
        var admin = Admin();

        var probe = await admin.GetAsync("/system/integrations/openbao/status?probe=true");
        Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        var probed = await probe.Content.ReadFromJsonAsync<IntegrationLinkDto>();
        Assert.NotNull(probed!.CheckedAtUtc);

        var settings = await admin.GetFromJsonAsync<SystemSettingsDto>("/system/settings");
        var openBao = settings!.Integrations.Single(i => i.Key == "openbao");
        Assert.NotNull(openBao.CheckedAtUtc);
    }

    [Fact]
    public async Task Reader_cannot_save_or_test_mail_settings()
    {
        var reader = Reader();
        var mail = new { smtpHost = "smtp.example.com", smtpPort = 587, smtpUsername = (string?)null, smtpPassword = "pw", fromAddress = "a@b.com", fromDisplayName = (string?)null, enableSsl = true };

        var save = await reader.PutAsJsonAsync("/system/settings/mail", mail);
        var test = await reader.PostAsJsonAsync("/system/settings/mail/test", new { mail, testRecipient = "a@b.com" });

        Assert.Equal(HttpStatusCode.Forbidden, save.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, test.StatusCode);
    }

    [Fact]
    public async Task Admin_can_save_mail_settings_and_they_take_effect_immediately()
    {
        var admin = Admin();
        var mail = new { smtpHost = "smtp.example.com", smtpPort = 587, smtpUsername = "no-reply", smtpPassword = "pw", fromAddress = "no-reply@example.com", fromDisplayName = "Iris", enableSsl = true };

        var save = await admin.PutAsJsonAsync("/system/settings/mail", mail);

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = await save.Content.ReadFromJsonAsync<SavedDto>();
        // Unlike OpenBao/AWX/Ansible: SmtpEmailSender reads settings fresh on every send, no
        // startup-only options singleton to restart for.
        Assert.False(saved!.RestartRequired);

        var settings = await admin.GetFromJsonAsync<SystemSettingsDto>("/system/settings");
        Assert.NotNull(settings!.Mail);
    }

    [Fact]
    public async Task Admin_can_send_a_test_email()
    {
        var admin = Admin();
        var mail = new { smtpHost = "smtp.example.com", smtpPort = 587, smtpUsername = (string?)null, smtpPassword = "pw", fromAddress = "no-reply@example.com", fromDisplayName = (string?)null, enableSsl = true };

        var response = await admin.PostAsJsonAsync("/system/settings/mail/test", new { mail, testRecipient = "someone@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reader_cannot_save_any_integration_settings()
    {
        var reader = Reader();

        var openBao = await reader.PutAsJsonAsync("/system/integrations/openbao",
            new { endpoint = "https://openbao.example.com", token = "t", mountPath = "secret", useKvV2 = true });
        var awx = await reader.PutAsJsonAsync("/system/integrations/awx",
            new { endpoint = "https://awx.example.com", token = "t", jobTemplateId = 1 });
        var ansible = await reader.PutAsJsonAsync("/system/integrations/ansible",
            new { endpoint = "https://ansible.example.com", playbook = "deploy.yml", inventory = (string?)null });
        var azureDevOps = await reader.PutAsJsonAsync("/system/integrations/azure-devops",
            new { endpoint = "https://dev.azure.com/contoso", token = "t" });
        var nexus = await reader.PutAsJsonAsync("/system/integrations/nexus",
            new { endpoint = "https://nexus.example.com", token = "t" });

        Assert.Equal(HttpStatusCode.Forbidden, openBao.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, awx.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ansible.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, azureDevOps.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, nexus.StatusCode);
    }

    [Fact]
    public async Task Admin_can_save_azure_devops_and_nexus_settings_and_they_show_up_in_settings()
    {
        var admin = Admin();

        var azureDevOps = await admin.PutAsJsonAsync("/system/integrations/azure-devops",
            new { endpoint = "https://dev.azure.com/contoso", token = "pat-token" });
        var nexus = await admin.PutAsJsonAsync("/system/integrations/nexus",
            new { endpoint = "https://nexus.example.com", token = "nexus-token" });

        Assert.Equal(HttpStatusCode.OK, azureDevOps.StatusCode);
        Assert.Equal(HttpStatusCode.OK, nexus.StatusCode);

        var settings = await admin.GetFromJsonAsync<SystemSettingsDto>("/system/settings");
        Assert.Contains(settings!.Integrations, i => i.Key == "azure-devops");
        Assert.Contains(settings.Integrations, i => i.Key == "nexus");
    }

    [Fact]
    public async Task Admin_can_save_OpenBao_settings_and_sees_RestartRequired_afterward()
    {
        var admin = Admin();

        var save = await admin.PutAsJsonAsync("/system/integrations/openbao",
            new { endpoint = "https://openbao.example.com", token = "root-token", mountPath = "secret", useKvV2 = true });

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = await save.Content.ReadFromJsonAsync<SavedDto>();
        Assert.True(saved!.RestartRequired);

        // The running test host already locked in its OpenBaoOptions singleton at startup, from
        // whatever appsettings the test host boots with — by design (see RegisterIntegrations /
        // ActiveIntegrationSnapshot), a save afterward does not change what that live connector
        // reports; only RestartRequired flips, until the process actually restarts. Asserting the
        // connector's own reported Endpoint changed here would contradict that design.
        var settings = await admin.GetFromJsonAsync<SystemSettingsDto>("/system/settings");
        Assert.True(settings!.RestartRequired);
        Assert.Contains(settings.Integrations, i => i.Key == "openbao");
    }

    [Fact]
    public async Task Admin_cannot_save_a_blank_endpoint()
    {
        var admin = Admin();

        var response = await admin.PutAsJsonAsync("/system/integrations/awx",
            new { endpoint = "", token = (string?)null, jobTemplateId = (int?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reader_cannot_provision_openbao()
    {
        var response = await Reader().PostAsync("/system/integrations/openbao/provision", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_provision_openbao_when_docker_is_available()
    {
        ContainerRuntime.IsAvailable = true;
        ContainerRuntime.Status = new ContainerStatus(ContainerState.Absent, null);
        ContainerRuntime.Logs = "==> OpenBao server started!\nRoot Token: s.integration-test-token\n";

        var response = await Admin().PostAsync("/system/integrations/openbao/provision", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var provisioned = await response.Content.ReadFromJsonAsync<ProvisionedDto>();
        Assert.Equal("http://localhost:8200", provisioned!.Endpoint);
        Assert.True(provisioned.RestartRequired);
    }

    [Fact]
    public async Task Admin_gets_a_clear_error_when_docker_is_not_available()
    {
        ContainerRuntime.IsAvailable = false;

        var response = await Admin().PostAsync("/system/integrations/openbao/provision", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_provisioning_restarts_a_previously_stopped_container_instead_of_failing()
    {
        // Regression test for a real bug found via manual testing (2026-09-08): "install for
        // me" used to hard-fail whenever this convenience container had been stopped since the
        // last provision, requiring a manual `docker rm` first.
        ContainerRuntime.IsAvailable = true;
        ContainerRuntime.Status = new ContainerStatus(ContainerState.Stopped, "existing-id");
        ContainerRuntime.Logs = "Root Token: s.restarted-token\n";

        var response = await Admin().PostAsync("/system/integrations/openbao/provision", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_gets_a_clear_error_when_the_root_token_cannot_be_found_in_logs()
    {
        ContainerRuntime.IsAvailable = true;
        ContainerRuntime.Status = new ContainerStatus(ContainerState.Absent, null);
        ContainerRuntime.Logs = "==> OpenBao server started, but no token line here.\n";

        var response = await Admin().PostAsync("/system/integrations/openbao/provision", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record SavedDto(bool RestartRequired, string Message);

    private sealed record ProvisionedDto(string Endpoint, bool RestartRequired, string Message);

    private sealed record IntegrationLinkDto(string Key, string Name, string Status, string? Endpoint, string? Message, DateTimeOffset? CheckedAtUtc = null);

    private sealed record SystemSettingsDto(bool CanManageSystem, object? Mail, List<IntegrationLinkDto> Integrations, bool RestartRequired);
}
