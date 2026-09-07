using Iris.Application.Common;
using Iris.Application.Deployments;
using Iris.Application.Tests.Fakes;
using Iris.Domain.Applications;
using Iris.Domain.Infrastructure;
using Iris.Domain.Tenancy;

namespace Iris.Application.Tests.Deployments;

public sealed class EnvironmentServerAssignmentHandlersTests
{
    private static AssignServerToEnvironmentHandler AssignHandler(FakeStore store) =>
        new(store.CustomerRepository, store.ServerRepository, store.EnvironmentServerAssignmentRepository, store.UnitOfWork);

    private static ListEnvironmentServerAssignmentsHandler ListHandler(FakeStore store) =>
        new(store.EnvironmentServerAssignmentRepository, store.CustomerRepository, store.ServerRepository);

    private static UnassignServerFromEnvironmentHandler UnassignHandler(FakeStore store) =>
        new(store.EnvironmentServerAssignmentRepository, store.ApplicationInstallationRepository, store.UnitOfWork);

    private static (Customer Customer, CustomerContext Context, ServerNode Server) SeedEnvironmentAndServer(FakeStore store)
    {
        var customer = new Customer(Guid.CreateVersion7(), "contoso", "Contoso Ltd.");
        var context = customer.AddContext(Guid.CreateVersion7(), "Production", ContextKind.Production);
        store.WithCustomer(customer);

        var server = new ServerNode(
            Guid.CreateVersion7(), "engine01", "engine01.example",
            ServerOs.Linux, ServerHostingType.Cloud, null, "10.0.0.12", ContextKind.Production);
        store.WithServer(server);

        return (customer, context, server);
    }

    [Fact]
    public async Task AssignServerToEnvironment_creates_an_assignment()
    {
        var store = new FakeStore();
        var (customer, context, server) = SeedEnvironmentAndServer(store);

        var response = await AssignHandler(store).HandleAsync(
            new AssignServerToEnvironmentCommand(context.Id, server.Id, "primary engine host"));

        Assert.Equal(customer.Id, response.CustomerId);
        Assert.Equal(context.Id, response.CustomerContextId);
        Assert.Equal(server.Id, response.ServerNodeId);
        Assert.Equal("engine01", response.ServerName);
        Assert.Equal("Production", response.Environment);
        Assert.Equal("primary engine host", response.Notes);
        Assert.Single(store.EnvironmentServerAssignments);
    }

    [Fact]
    public async Task AssignServerToEnvironment_rejects_a_duplicate_assignment()
    {
        var store = new FakeStore();
        var (_, context, server) = SeedEnvironmentAndServer(store);
        await AssignHandler(store).HandleAsync(new AssignServerToEnvironmentCommand(context.Id, server.Id, null));

        await Assert.ThrowsAsync<ConflictException>(() =>
            AssignHandler(store).HandleAsync(new AssignServerToEnvironmentCommand(context.Id, server.Id, null)));

        Assert.Single(store.EnvironmentServerAssignments);
    }

    [Fact]
    public async Task AssignServerToEnvironment_requires_a_known_server()
    {
        var store = new FakeStore();
        var (_, context, _) = SeedEnvironmentAndServer(store);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            AssignHandler(store).HandleAsync(new AssignServerToEnvironmentCommand(context.Id, Guid.NewGuid(), null)));
    }

    [Fact]
    public async Task ListEnvironmentServerAssignments_returns_every_assignment()
    {
        var store = new FakeStore();
        var (_, context, server) = SeedEnvironmentAndServer(store);
        await AssignHandler(store).HandleAsync(new AssignServerToEnvironmentCommand(context.Id, server.Id, null));

        var list = await ListHandler(store).HandleAsync(new ListEnvironmentServerAssignmentsQuery());

        var assignment = Assert.Single(list);
        Assert.Equal(server.Id, assignment.ServerNodeId);
        Assert.Equal(context.Id, assignment.CustomerContextId);
    }

    [Fact]
    public async Task UnassignServerFromEnvironment_removes_an_unused_assignment()
    {
        var store = new FakeStore();
        var (_, context, server) = SeedEnvironmentAndServer(store);
        var assignment = await AssignHandler(store).HandleAsync(new AssignServerToEnvironmentCommand(context.Id, server.Id, null));

        await UnassignHandler(store).HandleAsync(new UnassignServerFromEnvironmentCommand(assignment.Id));

        Assert.Empty(store.EnvironmentServerAssignments);
    }

    [Fact]
    public async Task UnassignServerFromEnvironment_rejects_when_installations_still_target_the_server()
    {
        var store = new FakeStore();
        var (_, context, server) = SeedEnvironmentAndServer(store);
        var assignment = await AssignHandler(store).HandleAsync(new AssignServerToEnvironmentCommand(context.Id, server.Id, null));

        var installation = new ApplicationInstallation(
            Guid.CreateVersion7(), "augeg4-engine-prd", Guid.CreateVersion7(), Guid.CreateVersion7(),
            null, null, server.Id, context.Id, null);
        store.ApplicationInstallations.Add(installation);

        await Assert.ThrowsAsync<ConflictException>(() =>
            UnassignHandler(store).HandleAsync(new UnassignServerFromEnvironmentCommand(assignment.Id)));

        Assert.Single(store.EnvironmentServerAssignments);
    }

    [Fact]
    public async Task UnassignServerFromEnvironment_requires_a_known_assignment() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            UnassignHandler(new FakeStore()).HandleAsync(new UnassignServerFromEnvironmentCommand(Guid.NewGuid())));
}
