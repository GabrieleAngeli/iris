using Iris.Application.Common;
using Iris.Application.Infrastructure;
using Iris.Application.Tests.Fakes;

namespace Iris.Application.Tests.Infrastructure;

public sealed class InfrastructureHandlersTests
{
    [Fact]
    public async Task CreateServer_requires_at_least_one_ip_address()
    {
        var store = new FakeStore();
        var handler = new CreateServerHandler(store.ServerRepository, store.UnitOfWork);

        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new CreateServerCommand(
            "web-01", "web-01.internal", "Linux", "SelfHosted", null, null, "Production")));
    }

    [Fact]
    public async Task CreateServer_persists_with_either_ip_present()
    {
        var store = new FakeStore();
        var handler = new CreateServerHandler(store.ServerRepository, store.UnitOfWork);

        var created = await handler.HandleAsync(new CreateServerCommand(
            "web-01", "web-01.internal", "Linux", "SelfHosted", null, "10.0.0.5", "Production"));

        Assert.Equal("web-01", created.Name);
        Assert.Equal("Linux", created.Os);
        Assert.Equal("SelfHosted", created.HostingType);
        Assert.Equal("Production", created.Environment);
        Assert.Empty(created.Credentials);
        Assert.Single(store.Servers);
    }

    [Fact]
    public async Task CreateServer_rejects_unknown_enum_values()
    {
        var store = new FakeStore();
        var handler = new CreateServerHandler(store.ServerRepository, store.UnitOfWork);

        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new CreateServerCommand(
            "web-01", null, "MacOS", "SelfHosted", "1.2.3.4", null, "Production")));
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "OnPrem", "1.2.3.4", null, "Production")));
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Prod")));
    }

    [Fact]
    public async Task AddServerCredential_stores_the_secret_via_the_store_not_in_the_domain()
    {
        var store = new FakeStore();
        var createHandler = new CreateServerHandler(store.ServerRepository, store.UnitOfWork);
        var server = await createHandler.HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production"));

        var addCredential = new AddServerCredentialHandler(store.ServerRepository, store.SecretStore, store.UnitOfWork);
        var credential = await addCredential.HandleAsync(new AddServerCredentialCommand(
            server.Id, "deploy", "SshKey", "-----BEGIN OPENSSH PRIVATE KEY-----super-secret", "Deploy service account"));

        Assert.Equal("deploy", credential.Username);
        Assert.Equal("SshKey", credential.AuthMethod);
        Assert.Equal("Deploy service account", credential.Label);

        var persisted = Assert.Single(store.Servers).Credentials.Single();
        Assert.NotEqual("-----BEGIN OPENSSH PRIVATE KEY-----super-secret", persisted.SecretReference);
        Assert.Contains(persisted.SecretReference, store.SecretsByReference.Keys);
        Assert.Equal("-----BEGIN OPENSSH PRIVATE KEY-----super-secret", store.SecretsByReference[persisted.SecretReference]);
    }

    [Fact]
    public async Task AddServerCredential_rejects_unknown_server_and_duplicate_username()
    {
        var store = new FakeStore();
        var addCredential = new AddServerCredentialHandler(store.ServerRepository, store.SecretStore, store.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() => addCredential.HandleAsync(
            new AddServerCredentialCommand(Guid.NewGuid(), "root", "Password", "hunter2", null)));

        var createHandler = new CreateServerHandler(store.ServerRepository, store.UnitOfWork);
        var server = await createHandler.HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production"));

        await addCredential.HandleAsync(new AddServerCredentialCommand(server.Id, "root", "Password", "hunter2", null));

        await Assert.ThrowsAsync<ConflictException>(() => addCredential.HandleAsync(
            new AddServerCredentialCommand(server.Id, "root", "Password", "hunter3", null)));

        // the rejected duplicate's secret must not be left dangling in the store
        Assert.Single(store.SecretsByReference);
    }

    [Fact]
    public async Task RemoveServerCredential_removes_it_and_its_secret()
    {
        var store = new FakeStore();
        var createHandler = new CreateServerHandler(store.ServerRepository, store.UnitOfWork);
        var server = await createHandler.HandleAsync(new CreateServerCommand(
            "web-01", null, "Linux", "SelfHosted", "1.2.3.4", null, "Production"));

        var addCredential = new AddServerCredentialHandler(store.ServerRepository, store.SecretStore, store.UnitOfWork);
        var credential = await addCredential.HandleAsync(
            new AddServerCredentialCommand(server.Id, "root", "Password", "hunter2", null));

        var removeCredential = new RemoveServerCredentialHandler(store.ServerRepository, store.SecretStore, store.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            removeCredential.HandleAsync(new RemoveServerCredentialCommand(server.Id, Guid.NewGuid())));

        await removeCredential.HandleAsync(new RemoveServerCredentialCommand(server.Id, credential.Id));

        Assert.Empty(Assert.Single(store.Servers).Credentials);
        Assert.Empty(store.SecretsByReference);
    }

    [Fact]
    public async Task ListServers_returns_servers_ordered_by_name_with_credentials()
    {
        var store = new FakeStore();
        var createHandler = new CreateServerHandler(store.ServerRepository, store.UnitOfWork);
        await createHandler.HandleAsync(new CreateServerCommand(
            "zeta", null, "Windows", "Cloud", "9.9.9.9", null, "Test"));
        var alpha = await createHandler.HandleAsync(new CreateServerCommand(
            "alpha", null, "Linux", "SelfHosted", "1.1.1.1", null, "Staging"));

        var addCredential = new AddServerCredentialHandler(store.ServerRepository, store.SecretStore, store.UnitOfWork);
        await addCredential.HandleAsync(new AddServerCredentialCommand(alpha.Id, "root", "Password", "hunter2", null));

        var listHandler = new ListServersHandler(store.ServerRepository);
        var result = await listHandler.HandleAsync(new ListServersQuery());

        Assert.Equal(["alpha", "zeta"], result.Select(s => s.Name));
        Assert.Single(result.Single(s => s.Name == "alpha").Credentials);
    }
}
