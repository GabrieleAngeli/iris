using System.Net;
using System.Net.Http.Json;

namespace Iris.Api.Tests;

public sealed class InfrastructureApiTests(IrisApiFactory factory) : IClassFixture<IrisApiFactory>
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

    [Fact]
    public async Task Reader_cannot_create_a_server()
    {
        var response = await Reader().PostAsJsonAsync("/servers", new
        {
            name = "nope",
            hostname = (string?)null,
            os = "Linux",
            hostingType = "SelfHosted",
            publicIpAddress = "1.2.3.4",
            privateIpAddress = (string?)null,
            environment = "Test",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_register_a_server_and_manage_its_credentials()
    {
        var admin = Admin();
        var name = "web-" + Guid.NewGuid().ToString("N")[..8];

        // 1. register the server
        var create = await admin.PostAsJsonAsync("/servers", new
        {
            name,
            hostname = $"{name}.internal",
            os = "Linux",
            hostingType = "SelfHosted",
            publicIpAddress = (string?)null,
            privateIpAddress = "10.0.4.12",
            environment = "Staging",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var server = await create.Content.ReadFromJsonAsync<ServerDto>();
        Assert.NotNull(server);
        Assert.Empty(server!.Credentials);

        // needs at least one IP -> 400
        var missingIp = await admin.PostAsJsonAsync("/servers", new
        {
            name = name + "-2",
            hostname = (string?)null,
            os = "Windows",
            hostingType = "Cloud",
            publicIpAddress = (string?)null,
            privateIpAddress = (string?)null,
            environment = "Test",
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingIp.StatusCode);

        // 2. add two independent OS-login credentials
        var addRoot = await admin.PostAsJsonAsync($"/servers/{server.Id}/credentials", new
        {
            username = "root",
            authMethod = "Password",
            secretValue = "correct-horse-battery-staple",
            label = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, addRoot.StatusCode);
        var rootCredential = await addRoot.Content.ReadFromJsonAsync<CredentialDto>();
        Assert.NotNull(rootCredential);

        var addDeploy = await admin.PostAsJsonAsync($"/servers/{server.Id}/credentials", new
        {
            username = "deploy",
            authMethod = "SshKey",
            secretValue = "-----BEGIN OPENSSH PRIVATE KEY-----",
            label = "CI deploy account",
        });
        Assert.Equal(HttpStatusCode.Created, addDeploy.StatusCode);

        // duplicate username on the same server -> 409
        var dup = await admin.PostAsJsonAsync($"/servers/{server.Id}/credentials", new
        {
            username = "root",
            authMethod = "Password",
            secretValue = "another-one",
            label = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        // 3. list shows both credentials, never a secret value
        var servers = await admin.GetFromJsonAsync<List<ServerDto>>("/servers");
        var listed = Assert.Single(servers!, s => s.Id == server.Id);
        Assert.Equal(2, listed.Credentials.Count);
        var listedJson = System.Text.Json.JsonSerializer.Serialize(listed);
        Assert.DoesNotContain("correct-horse-battery-staple", listedJson);
        Assert.DoesNotContain("BEGIN OPENSSH PRIVATE KEY", listedJson);

        // 4. remove one
        var remove = await admin.DeleteAsync($"/servers/{server.Id}/credentials/{rootCredential!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var removeAgain = await admin.DeleteAsync($"/servers/{server.Id}/credentials/{rootCredential.Id}");
        Assert.Equal(HttpStatusCode.NotFound, removeAgain.StatusCode);
    }

    private sealed record ServerDto(Guid Id, string Name, List<CredentialDto> Credentials);

    private sealed record CredentialDto(Guid Id, string Username, string AuthMethod, string? Label);
}
