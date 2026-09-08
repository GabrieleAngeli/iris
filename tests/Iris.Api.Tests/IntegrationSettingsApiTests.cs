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
    public async Task Reader_cannot_save_any_integration_settings()
    {
        var reader = Reader();

        var openBao = await reader.PutAsJsonAsync("/system/integrations/openbao",
            new { endpoint = "https://openbao.example.com", token = "t", mountPath = "secret", useKvV2 = true });
        var awx = await reader.PutAsJsonAsync("/system/integrations/awx",
            new { endpoint = "https://awx.example.com", token = "t", jobTemplateId = 1 });
        var ansible = await reader.PutAsJsonAsync("/system/integrations/ansible",
            new { endpoint = "https://ansible.example.com", playbook = "deploy.yml", inventory = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, openBao.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, awx.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ansible.StatusCode);
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

    private sealed record IntegrationLinkDto(string Key, string Name, string Status, string? Endpoint, string? Message);

    private sealed record SystemSettingsDto(bool CanManageSystem, object? Mail, List<IntegrationLinkDto> Integrations, bool RestartRequired);
}
