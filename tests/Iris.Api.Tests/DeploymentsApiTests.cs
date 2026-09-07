using System.Net;
using System.Net.Http.Json;

namespace Iris.Api.Tests;

public sealed class DeploymentsApiTests(IrisApiFactory factory) : IClassFixture<IrisApiFactory>
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

    private async Task<(Guid CustomerId, Guid ContextId)> SeedCustomerContextAsync(HttpClient admin)
    {
        var name = "cust-" + Guid.NewGuid().ToString("N")[..8];
        var customer = await (await admin.PostAsJsonAsync("/customers", new { key = name, name = "Test Customer" }))
            .Content.ReadFromJsonAsync<IdOnlyDto>();
        var context = await (await admin.PostAsJsonAsync($"/customers/{customer!.Id}/contexts", new { name = "Production", kind = "Production" }))
            .Content.ReadFromJsonAsync<IdOnlyDto>();
        return (customer.Id, context!.Id);
    }

    private async Task<Guid> SeedServerAsync(HttpClient admin, string suffix)
    {
        var server = await (await admin.PostAsJsonAsync("/servers", new
        {
            name = $"srv-{suffix}",
            os = "Linux",
            hostingType = "SelfHosted",
            privateIpAddress = "10.0.6.6",
            environment = "Production",
        })).Content.ReadFromJsonAsync<IdOnlyDto>();
        return server!.Id;
    }

    [Fact]
    public async Task Reader_cannot_assign_a_server_to_an_environment()
    {
        var response = await Reader().PostAsJsonAsync(
            $"/deployments/contexts/{Guid.NewGuid()}/server-assignments", new { serverNodeId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Assigning_an_unknown_server_returns_not_found()
    {
        var admin = Admin();
        var (_, contextId) = await SeedCustomerContextAsync(admin);

        var response = await admin.PostAsJsonAsync(
            $"/deployments/contexts/{contextId}/server-assignments", new { serverNodeId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_assign_list_and_unassign_a_server()
    {
        var admin = Admin();
        var (_, contextId) = await SeedCustomerContextAsync(admin);
        var serverId = await SeedServerAsync(admin, Guid.NewGuid().ToString("N")[..8]);

        var assign = await admin.PostAsJsonAsync(
            $"/deployments/contexts/{contextId}/server-assignments", new { serverNodeId = serverId, notes = "primary" });
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);
        var assignment = await assign.Content.ReadFromJsonAsync<AssignmentDto>();
        Assert.Equal(serverId, assignment!.ServerNodeId);
        Assert.Equal(contextId, assignment.CustomerContextId);

        // Duplicate assignment -> 409
        var duplicate = await admin.PostAsJsonAsync(
            $"/deployments/contexts/{contextId}/server-assignments", new { serverNodeId = serverId });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var list = await admin.GetFromJsonAsync<List<AssignmentDto>>("/deployments/server-assignments");
        Assert.Contains(list!, a => a.Id == assignment.Id);

        var unassign = await admin.DeleteAsync($"/deployments/server-assignments/{assignment.Id}");
        Assert.Equal(HttpStatusCode.NoContent, unassign.StatusCode);

        var listAfter = await admin.GetFromJsonAsync<List<AssignmentDto>>("/deployments/server-assignments");
        Assert.DoesNotContain(listAfter!, a => a.Id == assignment.Id);
    }

    [Fact]
    public async Task Unassigning_a_server_still_used_by_an_installation_returns_conflict()
    {
        var admin = Admin();
        var (_, contextId) = await SeedCustomerContextAsync(admin);
        var serverId = await SeedServerAsync(admin, Guid.NewGuid().ToString("N")[..8]);
        var assign = await (await admin.PostAsJsonAsync(
            $"/deployments/contexts/{contextId}/server-assignments", new { serverNodeId = serverId }))
            .Content.ReadFromJsonAsync<AssignmentDto>();

        var name = "svc-" + Guid.NewGuid().ToString("N")[..8];
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
            runtimeMetadata = new { runtimeName = "dotnet9", requiredPorts = Array.Empty<int>() },
        })).Content.ReadFromJsonAsync<IdOnlyDto>();
        await admin.PostAsJsonAsync($"/applications/{application.Id}/installations", new
        {
            name = $"{name}-prd",
            applicationVersionId = version!.Id,
            serverNodeId = serverId,
            customerContextId = contextId,
        });

        var unassign = await admin.DeleteAsync($"/deployments/server-assignments/{assign!.Id}");
        Assert.Equal(HttpStatusCode.Conflict, unassign.StatusCode);
    }

    private sealed record IdOnlyDto(Guid Id);

    private sealed record AssignmentDto(Guid Id, Guid CustomerContextId, Guid ServerNodeId, string ServerName, string? Notes);
}
