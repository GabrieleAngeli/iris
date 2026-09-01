using Iris.Application.Common;
using Iris.Application.Infrastructure;
using Iris.Application.Tests.Fakes;
using Iris.Domain.Access;

namespace Iris.Application.Tests.Infrastructure;

public sealed class InfrastructureHandlersTests
{
    private static ServerCredentialFactory Factory(FakeStore store) =>
        new(store.SecretStore, store.UserRepository);

    private static CreateServerHandler CreateHandler(FakeStore store) =>
        new(store.ServerRepository, Factory(store), store.UnitOfWork);

    private static AddServerCredentialHandler AddCredentialHandler(FakeStore store) =>
        new(store.ServerRepository, Factory(store), store.UnitOfWork);

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
}
