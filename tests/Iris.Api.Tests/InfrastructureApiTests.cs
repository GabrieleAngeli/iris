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
            resources = new { cpuCores = 4, memoryMb = 8192, diskGb = 250, applicationDiskGb = 150, backupDiskGb = 80 },
            usedPorts = new[] { 5432, 22, 22 },
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<ServerFullDto>();
        Assert.Equal(["Database", "ServiceHost"], updated!.Capabilities);
        Assert.Equal(4, updated.Resources!.CpuCores);
        Assert.Equal(250, updated.Resources.DiskGb);
        Assert.Equal(150, updated.Resources.ApplicationDiskGb);
        Assert.Equal(80, updated.Resources.BackupDiskGb);
        Assert.Equal([22, 5432], updated.UsedPorts);

        // unknown capability -> 400
        var bad = await admin.PutAsJsonAsync($"/servers/{server.Id}/capacity", new
        {
            capabilities = new[] { "FlyingCar" },
            resources = (object?)null,
            usedPorts = Array.Empty<int>(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var badDisks = await admin.PutAsJsonAsync($"/servers/{server.Id}/capacity", new
        {
            capabilities = Array.Empty<string>(),
            resources = new { diskGb = 100, applicationDiskGb = 80, backupDiskGb = 40 },
            usedPorts = Array.Empty<int>(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, badDisks.StatusCode);

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

    [Fact]
    public async Task Admin_can_discover_server_inventory_after_adding_credentials()
    {
        var admin = Admin();
        var name = "disc-" + Guid.NewGuid().ToString("N")[..8];

        var create = await admin.PostAsJsonAsync("/servers", new
        {
            name,
            os = "Linux",
            hostingType = "SelfHosted",
            privateIpAddress = "10.0.8.8",
            environment = "Production",
        });
        var server = await create.Content.ReadFromJsonAsync<ServerDto>();

        var noCredential = await admin.PostAsync($"/servers/{server!.Id}/discover", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, noCredential.StatusCode);

        await admin.PostAsJsonAsync($"/servers/{server.Id}/credentials", new
        {
            username = "ansible",
            authMethod = "SshKey",
            secretValue = "key",
            kind = "ServiceAccount",
            serviceName = "ansible",
        });

        var discover = await admin.PostAsync($"/servers/{server.Id}/discover", content: null);
        Assert.Equal(HttpStatusCode.OK, discover.StatusCode);
        var discovered = await discover.Content.ReadFromJsonAsync<ServerFullDto>();

        Assert.Equal("Ubuntu 22.04 LTS", discovered!.OsVersion);
        Assert.Equal("4 vCPU / 8 GB RAM", discovered.MachineSize);
        Assert.Equal(4, discovered.Resources!.CpuCores);
        Assert.Equal(160, discovered.Resources.ApplicationDiskGb);
        Assert.Equal(60, discovered.Resources.BackupDiskGb);
        Assert.Equal([22], discovered.UsedPorts);

        var readerDiscover = await Reader().PostAsync($"/servers/{server.Id}/discover", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, readerDiscover.StatusCode);
    }

    [Fact]
    public async Task Admin_can_manage_data_services_and_reader_cannot()
    {
        var admin = Admin();
        var name = "rds-" + Guid.NewGuid().ToString("N")[..8];

        var forbidden = await Reader().PostAsJsonAsync("/data-services", new
        {
            name,
            kind = "PostgreSql",
            endpoint = $"{name}.cluster.local",
            port = 5432,
            environment = "Production",
            username = "dbadmin",
            passwordValue = "top-secret",
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var create = await admin.PostAsJsonAsync("/data-services", new
        {
            name,
            kind = "PostgreSql",
            endpoint = $"{name}.cluster.local",
            port = 5432,
            version = "16",
            size = "db.t3.medium",
            storageGb = 100,
            environment = "Production",
            username = "dbadmin",
            passwordValue = "top-secret",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var dataService = await create.Content.ReadFromJsonAsync<DataServiceDto>();
        Assert.Equal("PostgreSql", dataService!.Kind);
        Assert.Equal("PostgreSQL 16", dataService.Version);
        Assert.Equal("dbadmin", dataService.Username);
        Assert.DoesNotContain("top-secret", System.Text.Json.JsonSerializer.Serialize(dataService));

        var update = await admin.PutAsJsonAsync($"/data-services/{dataService.Id}", new
        {
            name = name + "-cache",
            kind = "Redis",
            endpoint = $"{name}-cache.cluster.local",
            port = 6379,
            version = "7",
            size = "cache.t3.small",
            storageGb = 20,
            environment = "Staging",
            isActive = false,
            username = "cache-admin",
            passwordValue = "rotated-secret",
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<DataServiceDto>();
        Assert.Equal("Redis", updated!.Kind);
        Assert.Equal("Redis 7.2", updated.Version);
        Assert.Equal("cache-admin", updated.Username);
        Assert.False(updated.IsActive);

        var discover = await admin.PostAsync($"/data-services/{dataService.Id}/discover", content: null);
        Assert.Equal(HttpStatusCode.OK, discover.StatusCode);

        var listed = await admin.GetFromJsonAsync<List<DataServiceDto>>("/data-services");
        Assert.Contains(listed!, s => s.Id == dataService.Id);
    }

    private sealed record ServerDto(Guid Id, string Name, List<CredentialDto> Credentials);

    private sealed record ResourceProfileDto(
        int? CpuCores,
        int? MemoryMb,
        int? DiskGb,
        int? ApplicationDiskGb,
        int? BackupDiskGb);

    private sealed record ServerFullDto(
        Guid Id, string Name, string Os, string? OsVersion, string? MachineSize, string HostingType, string Environment,
        List<string> Capabilities, ResourceProfileDto? Resources, List<int> UsedPorts);

    private sealed record CredentialDto(
        Guid Id, string Username, string AuthMethod, string Kind,
        Guid? OwnerUserId, string? OwnerDisplayName, string? ServiceName, string? Label);

    private sealed record UserDto(Guid Id, string Email);

    private sealed record DataServiceDto(
        Guid Id,
        string Name,
        string Kind,
        string Endpoint,
        int? Port,
        string? Username,
        string? Version,
        string? Size,
        int? StorageGb,
        string Environment,
        bool IsActive);
}
