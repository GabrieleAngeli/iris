using System.Net;
using System.Net.Http.Json;

namespace Iris.Api.Tests;

public sealed class ActionsApiTests(IrisApiFactory factory) : IClassFixture<IrisApiFactory>
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

    /// <summary>Global-scoped, read-only (<c>actions.read</c> but not <c>deployments.prepare</c>/<c>actions.run</c>)
    /// — unlike <see cref="Reader"/> (customer-scoped), this isolates the permission check from the
    /// scope check for the Global-scoped Actions endpoints (no customerId/contextId route or query value).</summary>
    private HttpClient Auditor()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "sara@iris.local");
        return client;
    }

    private async Task<Guid> SeedInstallationAsync(HttpClient admin, string name)
    {
        var application = await (await admin.PostAsJsonAsync("/applications", new
        {
            name,
            runtimeType = "CSharp",
            repositoryUrl = $"https://git.example/{name}",
            defaultBranch = "main",
        })).Content.ReadFromJsonAsync<IdOnlyDto>();

        var version = await (await admin.PostAsJsonAsync($"/applications/{application!.Id}/versions", new
        {
            version = "1.0.0",
            runtimeMetadata = new { runtimeName = "dotnet9", requiredPorts = new[] { 8080 } },
        })).Content.ReadFromJsonAsync<IdOnlyDto>();

        var server = await (await admin.PostAsJsonAsync("/servers", new
        {
            name = $"{name}-node",
            os = "Linux",
            hostingType = "SelfHosted",
            privateIpAddress = "10.0.6.6",
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

        return installation!.Id;
    }

    [Fact]
    public async Task Reader_cannot_prepare_execute_or_cancel_an_action()
    {
        var installationId = Guid.NewGuid();

        var prepare = await Reader().PostAsJsonAsync(
            $"/applications/installations/{installationId}/actions/prepare", new { });
        Assert.Equal(HttpStatusCode.Forbidden, prepare.StatusCode);

        var execute = await Reader().PostAsync($"/actions/{Guid.NewGuid()}/execute", null);
        Assert.Equal(HttpStatusCode.Forbidden, execute.StatusCode);

        var cancel = await Reader().PostAsJsonAsync($"/actions/{Guid.NewGuid()}/cancel", new { });
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
    }

    [Fact]
    public async Task Global_scoped_auditor_can_list_and_get_but_not_prepare_execute_or_cancel_actions()
    {
        var auditor = Auditor();

        var list = await auditor.GetAsync("/actions");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var get = await auditor.GetAsync($"/actions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        var prepare = await auditor.PostAsJsonAsync(
            $"/applications/installations/{Guid.NewGuid()}/actions/prepare", new { });
        Assert.Equal(HttpStatusCode.Forbidden, prepare.StatusCode);

        var execute = await auditor.PostAsync($"/actions/{Guid.NewGuid()}/execute", null);
        Assert.Equal(HttpStatusCode.Forbidden, execute.StatusCode);
    }

    [Fact]
    public async Task Admin_can_prepare_review_and_the_execute_reflects_awx_not_being_configured()
    {
        var admin = Admin();
        var installationId = await SeedInstallationAsync(admin, "svc-" + Guid.NewGuid().ToString("N")[..8]);

        var prepare = await admin.PostAsJsonAsync(
            $"/applications/installations/{installationId}/actions/prepare", new { });
        Assert.Equal(HttpStatusCode.Created, prepare.StatusCode);
        var prepared = await prepare.Content.ReadFromJsonAsync<PreparedActionDto>();
        Assert.Equal("Prepared", prepared!.Status);
        Assert.Equal(installationId, prepared.InstallationId);
        Assert.True(prepared.Validation.IsValid);

        var get = await admin.GetFromJsonAsync<PreparedActionDto>($"/actions/{prepared.Id}");
        Assert.Equal(prepared.Id, get!.Id);

        var list = await admin.GetFromJsonAsync<List<ActionSummaryDto>>("/actions");
        Assert.Contains(list!, a => a.Id == prepared.Id && a.EffectiveStatus == "Prepared");

        // AWX is not configured in the test host: Execute surfaces that as a client error and the
        // action is left Prepared (not silently marked Executed) so the operator can retry.
        var execute = await admin.PostAsync($"/actions/{prepared.Id}/execute", null);
        Assert.Equal(HttpStatusCode.BadRequest, execute.StatusCode);

        var stillPrepared = await admin.GetFromJsonAsync<PreparedActionDto>($"/actions/{prepared.Id}");
        Assert.Equal("Prepared", stillPrepared!.Status);
    }

    [Fact]
    public async Task Admin_can_cancel_a_prepared_action_and_cannot_execute_it_afterwards()
    {
        var admin = Admin();
        var installationId = await SeedInstallationAsync(admin, "svc-" + Guid.NewGuid().ToString("N")[..8]);
        var prepared = await (await admin.PostAsJsonAsync(
            $"/applications/installations/{installationId}/actions/prepare", new { }))
            .Content.ReadFromJsonAsync<PreparedActionDto>();

        var cancel = await admin.PostAsJsonAsync($"/actions/{prepared!.Id}/cancel", new { reason = "not needed" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var canceled = await cancel.Content.ReadFromJsonAsync<PreparedActionDto>();
        Assert.Equal("Canceled", canceled!.Status);
        Assert.Equal("not needed", canceled.CancelReason);

        var execute = await admin.PostAsync($"/actions/{prepared.Id}/execute", null);
        Assert.Equal(HttpStatusCode.BadRequest, execute.StatusCode);
    }

    [Fact]
    public async Task Preparing_an_action_for_an_unknown_installation_returns_not_found()
    {
        var admin = Admin();

        var prepare = await admin.PostAsJsonAsync(
            $"/applications/installations/{Guid.NewGuid()}/actions/prepare", new { });

        Assert.Equal(HttpStatusCode.NotFound, prepare.StatusCode);
    }

    private sealed record IdOnlyDto(Guid Id);

    private sealed record ValidationDto(bool IsValid, int Errors, int Warnings, int Infos);

    private sealed record PreparedActionDto(
        Guid Id, Guid InstallationId, string Status, ValidationDto Validation,
        Guid? InstallationRunId, string? CancelReason);

    private sealed record ActionSummaryDto(Guid Id, Guid InstallationId, string EffectiveStatus);
}
