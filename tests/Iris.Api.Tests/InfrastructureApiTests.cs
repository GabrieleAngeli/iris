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
            os = "Linux",
            hostingType = "SelfHosted",
            publicIpAddress = "1.2.3.4",
            environment = "Test",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Server_can_be_registered_with_its_first_credential_in_one_call()
    {
        var admin = Admin();
        var name = "svc-" + Guid.NewGuid().ToString("N")[..8];

        var create = await admin.PostAsJsonAsync("/servers", new
        {
            name,
            os = "Linux",
            hostingType = "SelfHosted",
            privateIpAddress = "10.0.9.9",
            environment = "Production",
            credential = new
            {
                username = "ansible",
                authMethod = "SshKey",
                secretValue = "-----BEGIN OPENSSH PRIVATE KEY-----abc",
                kind = "ServiceAccount",
                serviceName = "ansible",
            },
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var server = await create.Content.ReadFromJsonAsync<ServerDto>();
        var cred = Assert.Single(server!.Credentials);
        Assert.Equal("ansible", cred.Username);
        Assert.Equal("ServiceAccount", cred.Kind);
        Assert.Equal("ansible", cred.ServiceName);
        Assert.DoesNotContain("BEGIN OPENSSH", System.Text.Json.JsonSerializer.Serialize(server));
    }

    [Fact]
    public async Task Admin_can_edit_and_delete_a_server()
    {
        var admin = Admin();
        var name = "edit-" + Guid.NewGuid().ToString("N")[..8];

        var create = await admin.PostAsJsonAsync("/servers", new
        {
            name, os = "Linux", hostingType = "SelfHosted", privateIpAddress = "10.0.1.1", environment = "Test",
        });
        var server = await create.Content.ReadFromJsonAsync<ServerDto>();

        // edit
        var edit = await admin.PutAsJsonAsync($"/servers/{server!.Id}", new
        {
            name = name + "-prod", hostname = $"{name}.corp", os = "Windows", hostingType = "Cloud",
            publicIpAddress = "203.0.113.9", privateIpAddress = (string?)null, environment = "Production",
        });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await edit.Content.ReadFromJsonAsync<ServerFullDto>();
        Assert.Equal(name + "-prod", edited!.Name);
        Assert.Equal("Windows", edited.Os);
        Assert.Equal("Production", edited.Environment);

        // reader can't edit or delete
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Reader().PutAsJsonAsync($"/servers/{server.Id}", new { name = "x", os = "Linux", hostingType = "SelfHosted", publicIpAddress = "1.1.1.1", environment = "Test" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Reader().DeleteAsync($"/servers/{server.Id}")).StatusCode);

        // delete
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/servers/{server.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/servers/{server.Id}")).StatusCode);

        var servers = await admin.GetFromJsonAsync<List<ServerDto>>("/servers");
        Assert.DoesNotContain(servers!, s => s.Id == server.Id);
    }

    [Fact]
    public async Task Admin_can_set_a_servers_capacity_and_reader_cannot()
    {
        var admin = Admin();
        var name = "cap-" + Guid.NewGuid().ToString("N")[..8];

        var create = await admin.PostAsJsonAsync("/servers", new
        {
            name, os = "Linux", hostingType = "SelfHosted", privateIpAddress = "10.0.2.2", environment = "Test",
        });
        var server = await create.Content.ReadFromJsonAsync<ServerDto>();

        var forbidden = await Reader().PutAsJsonAsync($"/servers/{server!.Id}/capacity", new
        {
            capabilities = new[] { "Database" },
            resources = (object?)null,
            usedPorts = Array.Empty<int>(),
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var update = await admin.PutAsJsonAsync($"/servers/{server.Id}/capacity", new
        {
            capabilities = new[] { "Database", "ServiceHost" },
            resources = new { cpuCores = 4, memoryMb = 8192, diskGb = 100 },
            usedPorts = new[] { 5432, 22, 22 },
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<ServerFullDto>();
        Assert.Equal(["Database", "ServiceHost"], updated!.Capabilities);
        Assert.Equal(4, updated.Resources!.CpuCores);
        Assert.Equal([22, 5432], updated.UsedPorts);

        // unknown capability -> 400
        var bad = await admin.PutAsJsonAsync($"/servers/{server.Id}/capacity", new
        {
            capabilities = new[] { "FlyingCar" },
            resources = (object?)null,
            usedPorts = Array.Empty<int>(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        // a second call replaces rather than accumulates
        var cleared = await admin.PutAsJsonAsync($"/servers/{server.Id}/capacity", new
        {
            capabilities = Array.Empty<string>(),
            resources = (object?)null,
            usedPorts = Array.Empty<int>(),
        });
        var clearedDto = await cleared.Content.ReadFromJsonAsync<ServerFullDto>();
        Assert.Empty(clearedDto!.Capabilities);
        Assert.Null(clearedDto.Resources);
        Assert.Empty(clearedDto.UsedPorts);
    }

    [Fact]
    public async Task Admin_can_register_a_server_and_manage_its_credentials()
    {
        var admin = Admin();
        var name = "web-" + Guid.NewGuid().ToString("N")[..8];

        var create = await admin.PostAsJsonAsync("/servers", new
        {
            name,
            hostname = $"{name}.internal",
            os = "Linux",
            hostingType = "SelfHosted",
            privateIpAddress = "10.0.4.12",
            environment = "Staging",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var server = await create.Content.ReadFromJsonAsync<ServerDto>();
        Assert.Empty(server!.Credentials);

        // needs at least one IP -> 400
        var missingIp = await admin.PostAsJsonAsync("/servers", new
        {
            name = name + "-2", os = "Windows", hostingType = "Cloud", environment = "Test",
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingIp.StatusCode);

        // a system-user credential linked to an Iris user
        var users = await admin.GetFromJsonAsync<List<UserDto>>("/governance/users");
        var lucia = Assert.Single(users!, u => u.Email == "lucia@contoso.example");

        var addLucia = await admin.PostAsJsonAsync($"/servers/{server.Id}/credentials", new
        {
            username = "lucia",
            authMethod = "SshKey",
            secretValue = "-----BEGIN OPENSSH PRIVATE KEY-----",
            kind = "SystemUser",
            ownerUserId = lucia.Id,
        });
        Assert.Equal(HttpStatusCode.Created, addLucia.StatusCode);
        var luciaCred = await addLucia.Content.ReadFromJsonAsync<CredentialDto>();
        Assert.Equal(lucia.Id, luciaCred!.OwnerUserId);
        Assert.Equal("Lucia Bianchi", luciaCred.OwnerDisplayName);

        var addRoot = await admin.PostAsJsonAsync($"/servers/{server.Id}/credentials", new
        {
            username = "root", authMethod = "Password", secretValue = "correct-horse", kind = "SystemUser",
        });
        Assert.Equal(HttpStatusCode.Created, addRoot.StatusCode);
        var rootCredential = await addRoot.Content.ReadFromJsonAsync<CredentialDto>();

        // duplicate username on the same server -> 409
        var dup = await admin.PostAsJsonAsync($"/servers/{server.Id}/credentials", new
        {
            username = "root", authMethod = "Password", secretValue = "another", kind = "SystemUser",
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        // list shows both, never a secret
        var servers = await admin.GetFromJsonAsync<List<ServerDto>>("/servers");
        var listed = Assert.Single(servers!, s => s.Id == server.Id);
        Assert.Equal(2, listed.Credentials.Count);
        Assert.DoesNotContain("correct-horse", System.Text.Json.JsonSerializer.Serialize(listed));

        // remove one
        var remove = await admin.DeleteAsync($"/servers/{server.Id}/credentials/{rootCredential!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var removeAgain = await admin.DeleteAsync($"/servers/{server.Id}/credentials/{rootCredential.Id}");
        Assert.Equal(HttpStatusCode.NotFound, removeAgain.StatusCode);
    }

    private sealed record ServerDto(Guid Id, string Name, List<CredentialDto> Credentials);

    private sealed record ResourceProfileDto(int? CpuCores, int? MemoryMb, int? DiskGb);

    private sealed record ServerFullDto(
        Guid Id, string Name, string Os, string HostingType, string Environment,
        List<string> Capabilities, ResourceProfileDto? Resources, List<int> UsedPorts);

    private sealed record CredentialDto(
        Guid Id, string Username, string AuthMethod, string Kind,
        Guid? OwnerUserId, string? OwnerDisplayName, string? ServiceName, string? Label);

    private sealed record UserDto(Guid Id, string Email);
}
