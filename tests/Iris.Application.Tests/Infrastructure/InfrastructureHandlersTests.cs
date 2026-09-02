using Iris.Application.Common;
using Iris.Application.Abstractions;
using Iris.Application.Infrastructure;
using Iris.Application.Tests.Fakes;
using Iris.Contracts.Infrastructure;
using Iris.Domain.Access;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Tests.Infrastructure;

public sealed class InfrastructureHandlersTests
{
    private static ServerCredentialFactory Factory(FakeStore store) =>
        new(store.SecretStore, store.UserRepository);

    private static CreateServerHandler CreateHandler(FakeStore store) =>
        new(store.ServerRepository, Factory(store), store.UnitOfWork);

    private static AddServerCredentialHandler AddCredentialHandler(FakeStore store) =>
        new(store.ServerRepository, Factory(store), store.UnitOfWork);

    private static CreateDataServiceHandler CreateDataServiceHandler(FakeStore store) =>
        new(store.DataServiceRepository, store.SecretStore, new StubDataServiceInventoryProbe(), store.UnitOfWork);

    private static UpdateDataServiceHandler UpdateDataServiceHandler(FakeStore store) =>
        new(store.DataServiceRepository, store.SecretStore, new StubDataServiceInventoryProbe(), store.UnitOfWork);

    private static ServerCredentialInput ServiceCred(string username = "deploy", string secret = "sshkey-material") =>
        new(username, "SshKey", secret, "ServiceAccount", null, "ansible", "CI deploy account");

    [Fact]
    public async Task CreateServer_requires_at_least_one_ip_address()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", "web-01.internal", "Linux", "SelfHosted", null, null, "Production")));
    }

    [Fact]
    public async Task CreateServer_persists_with_either_ip_present()
    {
        var store = new FakeStore();

        var created = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", "web-01.internal", "Linux", "SelfHosted", null, "10.0.0.5", "Production"));

        Assert.Equal("web-01", created.Name);
        Assert.Equal("Linux", created.Os);
        Assert.Empty(created.Credentials);
        Assert.Single(store.Servers);
    }

    [Fact]
    public async Task CreateServer_rejects_unknown_enum_values()
    {
        var store = new FakeStore();

        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "MacOS", "SelfHosted", "1.2.3.4", null, "Production")));
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "OnPrem", "1.2.3.4", null, "Production")));
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Prod")));
    }

    [Fact]
    public async Task CreateServer_can_register_the_first_credential_in_one_call()
    {
        var store = new FakeStore();

        var created = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production",
            new ServerCredentialInput("root", "Password", "hunter2", "SystemUser", null, null, null)));

        var credential = Assert.Single(created.Credentials);
        Assert.Equal("root", credential.Username);
        Assert.Equal("SystemUser", credential.Kind);
        Assert.Single(store.SecretsByReference);
    }

    [Fact]
    public async Task CreateServer_links_a_system_user_credential_to_an_iris_user()
    {
        var store = new FakeStore();
        var lucia = new User(Guid.NewGuid(), "ext-lucia", "lucia@contoso.example", "Lucia Bianchi");
        store.WithUser(lucia);

        var created = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production",
            new ServerCredentialInput("lucia", "SshKey", "key", "SystemUser", lucia.Id, null, null)));

        var credential = Assert.Single(created.Credentials);
        Assert.Equal(lucia.Id, credential.OwnerUserId);
        Assert.Equal("Lucia Bianchi", credential.OwnerDisplayName);
    }

    [Fact]
    public async Task Credential_kind_rules_are_enforced()
    {
        var store = new FakeStore();
        var server = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production"));
        var add = AddCredentialHandler(store);

        // ServiceAccount needs a service name
        await Assert.ThrowsAsync<ValidationException>(() => add.HandleAsync(new AddServerCredentialCommand(
            server.Id, "svc", "Password", "s", "ServiceAccount", null, null, null)));

        // ServiceAccount cannot be linked to an Iris user
        await Assert.ThrowsAsync<ValidationException>(() => add.HandleAsync(new AddServerCredentialCommand(
            server.Id, "svc", "Password", "s", "ServiceAccount", Guid.NewGuid(), "ansible", null)));

        // SystemUser owner must exist
        await Assert.ThrowsAsync<NotFoundException>(() => add.HandleAsync(new AddServerCredentialCommand(
            server.Id, "ghost", "Password", "s", "SystemUser", Guid.NewGuid(), null, null)));

        // unknown kind
        await Assert.ThrowsAsync<ValidationException>(() => add.HandleAsync(new AddServerCredentialCommand(
            server.Id, "x", "Password", "s", "Robot", null, null, null)));

        Assert.Empty(store.SecretsByReference); // every rejected attempt cleaned up its secret
    }

    [Fact]
    public async Task AddServerCredential_stores_the_secret_via_the_store_not_in_the_domain()
    {
        var store = new FakeStore();
        var server = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production"));

        var credential = await AddCredentialHandler(store).HandleAsync(new AddServerCredentialCommand(
            server.Id, "deploy", "SshKey", "-----BEGIN OPENSSH PRIVATE KEY-----super-secret",
            "ServiceAccount", null, "ansible", "Deploy service account"));

        Assert.Equal("deploy", credential.Username);
        Assert.Equal("SshKey", credential.AuthMethod);
        Assert.Equal("ServiceAccount", credential.Kind);
        Assert.Equal("ansible", credential.ServiceName);

        var persisted = Assert.Single(store.Servers).Credentials.Single();
        Assert.NotEqual("-----BEGIN OPENSSH PRIVATE KEY-----super-secret", persisted.SecretReference);
        Assert.Equal("-----BEGIN OPENSSH PRIVATE KEY-----super-secret", store.SecretsByReference[persisted.SecretReference]);
    }

    [Fact]
    public async Task AddServerCredential_rejects_unknown_server_and_duplicate_username()
    {
        var store = new FakeStore();
        var add = AddCredentialHandler(store);

        await Assert.ThrowsAsync<NotFoundException>(() => add.HandleAsync(
            new AddServerCredentialCommand(Guid.NewGuid(), "root", "Password", "hunter2", "SystemUser", null, null, null)));

        var server = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production"));

        await add.HandleAsync(new AddServerCredentialCommand(
            server.Id, "root", "Password", "hunter2", "SystemUser", null, null, null));

        await Assert.ThrowsAsync<ConflictException>(() => add.HandleAsync(
            new AddServerCredentialCommand(server.Id, "root", "Password", "hunter3", "SystemUser", null, null, null)));

        Assert.Single(store.SecretsByReference);
    }

    [Fact]
    public async Task UpdateServer_changes_details_and_validates()
    {
        var store = new FakeStore();
        var server = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", "web-01.internal", "Linux", "SelfHosted", "1.2.3.4", null, "Test"));

        var update = new UpdateServerHandler(store.ServerRepository, store.UserRepository, store.UnitOfWork);

        var updated = await update.HandleAsync(new UpdateServerCommand(
            server.Id, "web-01-renamed", null, "Windows", "Cloud", null, "10.0.0.9", "Production"));

        Assert.Equal("web-01-renamed", updated.Name);
        Assert.Equal("Windows", updated.Os);
        Assert.Equal("Cloud", updated.HostingType);
        Assert.Equal("Production", updated.Environment);
        Assert.Null(updated.PublicIpAddress);
        Assert.Equal("10.0.0.9", updated.PrivateIpAddress);

        await Assert.ThrowsAsync<NotFoundException>(() => update.HandleAsync(new UpdateServerCommand(
            Guid.NewGuid(), "x", null, "Linux", "SelfHosted", "1.1.1.1", null, "Test")));
        await Assert.ThrowsAsync<ValidationException>(() => update.HandleAsync(new UpdateServerCommand(
            server.Id, "x", null, "Linux", "SelfHosted", null, null, "Test")));
    }

    [Fact]
    public async Task DeleteServer_removes_the_server_and_purges_its_secrets()
    {
        var store = new FakeStore();
        var server = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production",
            new ServerCredentialInput("root", "Password", "hunter2", "SystemUser", null, null, null)));

        await AddCredentialHandler(store).HandleAsync(new AddServerCredentialCommand(
            server.Id, "deploy", "SshKey", "key", "ServiceAccount", null, "ansible", null));
        Assert.Equal(2, store.SecretsByReference.Count);

        var delete = new DeleteServerHandler(store.ServerRepository, store.SecretStore, store.UnitOfWork);

        await delete.HandleAsync(new DeleteServerCommand(server.Id));

        Assert.Empty(store.Servers);
        Assert.Empty(store.SecretsByReference);

        await Assert.ThrowsAsync<NotFoundException>(() => delete.HandleAsync(new DeleteServerCommand(server.Id)));
    }

    [Fact]
    public async Task UpdateServerCapacity_sets_capabilities_resources_and_ports()
    {
        var store = new FakeStore();
        var server = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production"));

        var update = new UpdateServerCapacityHandler(store.ServerRepository, store.UserRepository, store.UnitOfWork);

        var updated = await update.HandleAsync(new UpdateServerCapacityCommand(
            server.Id,
            ["Database", "ServiceHost"],
            new ResourceProfileRequest(4, 8192, 250, 150, 80),
            [5432, 22, 22]));

        Assert.Equal(["Database", "ServiceHost"], updated.Capabilities);
        Assert.Equal(4, updated.Resources!.CpuCores);
        Assert.Equal(8192, updated.Resources.MemoryMb);
        Assert.Equal(250, updated.Resources.DiskGb);
        Assert.Equal(150, updated.Resources.ApplicationDiskGb);
        Assert.Equal(80, updated.Resources.BackupDiskGb);
        Assert.Equal([22, 5432], updated.UsedPorts); // deduplicated and ordered
    }

    [Fact]
    public async Task UpdateServerCapacity_rejects_unknown_capability_and_missing_server()
    {
        var store = new FakeStore();
        var update = new UpdateServerCapacityHandler(store.ServerRepository, store.UserRepository, store.UnitOfWork);

        var server = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production"));

        await Assert.ThrowsAsync<ValidationException>(() => update.HandleAsync(new UpdateServerCapacityCommand(
            server.Id, ["FlyingCar"], null, [])));

        await Assert.ThrowsAsync<ValidationException>(() => update.HandleAsync(new UpdateServerCapacityCommand(
            server.Id, [], new ResourceProfileRequest(null, null, 100, 80, 40), [])));

        await Assert.ThrowsAsync<NotFoundException>(() => update.HandleAsync(new UpdateServerCapacityCommand(
            Guid.NewGuid(), [], null, [])));
    }

    [Fact]
    public async Task UpdateServerCapacity_replaces_rather_than_accumulates()
    {
        var store = new FakeStore();
        var update = new UpdateServerCapacityHandler(store.ServerRepository, store.UserRepository, store.UnitOfWork);
        var server = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production"));

        await update.HandleAsync(new UpdateServerCapacityCommand(
            server.Id, ["Database"], new ResourceProfileRequest(2, 4096, 50, 30, 10), [5432]));

        var cleared = await update.HandleAsync(new UpdateServerCapacityCommand(server.Id, [], null, []));

        Assert.Empty(cleared.Capabilities);
        Assert.Null(cleared.Resources);
        Assert.Empty(cleared.UsedPorts);
    }

    [Fact]
    public async Task DiscoverServerInventory_requires_credentials_and_updates_server()
    {
        var store = new FakeStore();
        var server = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production"));
        var discover = new DiscoverServerInventoryHandler(
            store.ServerRepository,
            store.UserRepository,
            new StubServerInventoryProbe(),
            store.UnitOfWork);

        await Assert.ThrowsAsync<ValidationException>(() =>
            discover.HandleAsync(new DiscoverServerInventoryCommand(server.Id)));

        await AddCredentialHandler(store).HandleAsync(new AddServerCredentialCommand(
            server.Id, "deploy", "SshKey", "key", "ServiceAccount", null, "ansible", null));

        var discovered = await discover.HandleAsync(new DiscoverServerInventoryCommand(server.Id));

        Assert.Equal("Ubuntu 24.04 LTS", discovered.OsVersion);
        Assert.Equal("D4 test shape", discovered.MachineSize);
        Assert.Equal(8, discovered.Resources!.CpuCores);
        Assert.Equal(300, discovered.Resources.DiskGb);
        Assert.Equal(210, discovered.Resources.ApplicationDiskGb);
        Assert.Equal(70, discovered.Resources.BackupDiskGb);
        Assert.Equal([22, 443], discovered.UsedPorts);
    }

    [Fact]
    public async Task DataServices_can_be_created_listed_and_updated()
    {
        var store = new FakeStore();

        var created = await CreateDataServiceHandler(store).HandleAsync(new CreateDataServiceCommand(
            "orders-postgres",
            "PostgreSql",
            "orders.cluster.local",
            5432,
            "16",
            "db.t3.medium",
            100,
            "Production",
            "dbadmin",
            "top-secret"));

        Assert.Equal("PostgreSql", created.Kind);
        Assert.Equal("dbadmin", created.Username);
        Assert.Equal(5432, created.Port);
        Assert.Equal("PostgreSQL 16 test", created.Version);
        Assert.Equal(128, created.StorageGb);
        Assert.Single(store.SecretsByReference);

        var updated = await UpdateDataServiceHandler(store).HandleAsync(new UpdateDataServiceCommand(
            created.Id,
            "orders-cache",
            "Redis",
            "redis.cluster.local",
            6379,
            "7",
            "cache.t3.small",
            20,
            "Staging",
            false,
            "cache-admin",
            "new-secret"));

        Assert.Equal("Redis", updated.Kind);
        Assert.Equal("redis.cluster.local", updated.Endpoint);
        Assert.Equal("cache-admin", updated.Username);
        Assert.Equal("Redis 7 test", updated.Version);
        Assert.False(updated.IsActive);

        var listed = await new ListDataServicesHandler(store.DataServiceRepository)
            .HandleAsync(new ListDataServicesQuery());
        Assert.Single(listed);
    }

    [Fact]
    public async Task DataServices_validate_kind_port_storage_and_environment()
    {
        var store = new FakeStore();
        var create = CreateDataServiceHandler(store);

        await Assert.ThrowsAsync<ValidationException>(() => create.HandleAsync(new CreateDataServiceCommand(
            "db", "Oracle", "db.local", 1521, null, null, null, "Test")));
        await Assert.ThrowsAsync<ValidationException>(() => create.HandleAsync(new CreateDataServiceCommand(
            "db", "Mssql", "db.local", 70000, null, null, null, "Test")));
        await Assert.ThrowsAsync<ValidationException>(() => create.HandleAsync(new CreateDataServiceCommand(
            "db", "Mssql", "db.local", 1433, null, null, -1, "Test")));
        await Assert.ThrowsAsync<ValidationException>(() => create.HandleAsync(new CreateDataServiceCommand(
            "db", "Mssql", "db.local", 1433, null, null, null, "Prod")));
        await Assert.ThrowsAsync<ValidationException>(() => create.HandleAsync(new CreateDataServiceCommand(
            "db", "Mssql", "db.local", 1433, null, null, null, "Test", null, "secret")));
        await Assert.ThrowsAsync<ValidationException>(() => create.HandleAsync(new CreateDataServiceCommand(
            "db", "Mssql", "db.local", 1433, null, null, null, "Test", "sa", null)));
    }

    [Fact]
    public async Task RemoveServerCredential_removes_it_and_its_secret()
    {
        var store = new FakeStore();
        var server = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production"));

        var credential = await AddCredentialHandler(store).HandleAsync(new AddServerCredentialCommand(
            server.Id, "root", "Password", "hunter2", "SystemUser", null, null, null));

        var remove = new RemoveServerCredentialHandler(store.ServerRepository, store.SecretStore, store.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            remove.HandleAsync(new RemoveServerCredentialCommand(server.Id, Guid.NewGuid())));

        await remove.HandleAsync(new RemoveServerCredentialCommand(server.Id, credential.Id));

        Assert.Empty(Assert.Single(store.Servers).Credentials);
        Assert.Empty(store.SecretsByReference);
    }

    [Fact]
    public async Task ListServers_returns_servers_ordered_by_name_with_credentials()
    {
        var store = new FakeStore();
        await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "zeta", null, "Windows", "Cloud", "9.9.9.9", null, "Test"));
        var alpha = await CreateHandler(store).HandleAsync(new CreateServerCommand(
            "alpha", null, "Linux", "SelfHosted", "1.1.1.1", null, "Staging"));

        await AddCredentialHandler(store).HandleAsync(new AddServerCredentialCommand(
            alpha.Id, "root", "Password", "hunter2", "SystemUser", null, null, null));

        var result = await new ListServersHandler(store.ServerRepository, store.UserRepository)
            .HandleAsync(new ListServersQuery());

        Assert.Equal(["alpha", "zeta"], result.Select(s => s.Name));
        Assert.Single(result.Single(s => s.Name == "alpha").Credentials);
    }

    private sealed class StubServerInventoryProbe : IServerInventoryProbe
    {
        public Task<ServerInventorySnapshot> DiscoverAsync(ServerNode server, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ServerInventorySnapshot(
                ServerOs.Linux,
                "Ubuntu 24.04 LTS",
                "D4 test shape",
                [NodeCapability.ServiceHost],
                new ResourceProfile(8, 16384, 300, 210, 70),
                [443, 22, 22]));
    }

    private sealed class StubDataServiceInventoryProbe : IDataServiceInventoryProbe
    {
        public Task<DataServiceInventorySnapshot> DiscoverAsync(
            DataServiceInstance dataService,
            CancellationToken cancellationToken = default)
        {
            var snapshot = dataService.Kind switch
            {
                DataServiceKind.Redis => new DataServiceInventorySnapshot(DataServiceKind.Redis, "Redis 7 test", "cache.test", 32),
                DataServiceKind.PostgreSql => new DataServiceInventorySnapshot(DataServiceKind.PostgreSql, "PostgreSQL 16 test", "db.test", 128),
                _ => new DataServiceInventorySnapshot(DataServiceKind.Mssql, "SQL Server 2022 test", "sql.test", 256),
            };

            return Task.FromResult(snapshot);
        }
    }
}
