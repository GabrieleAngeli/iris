using System.Net;
using System.Net.Http.Json;

namespace Iris.Api.Tests;

public sealed class GovernanceApiTests(IrisApiFactory factory) : IClassFixture<IrisApiFactory>
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
    public async Task Permission_catalog_is_available_to_any_authenticated_user()
    {
        var permissions = await Reader().GetFromJsonAsync<List<string>>("/governance/permissions");

        Assert.NotNull(permissions);
        Assert.Contains("platform.admin", permissions!);
        Assert.Contains("infrastructure.read", permissions!);
    }

    [Fact]
    public async Task Reader_cannot_create_a_customer()
    {
        var response = await Reader().PostAsJsonAsync("/customers", new { key = "nope", name = "Nope" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_run_the_full_governance_lifecycle()
    {
        var admin = Admin();
        var key = "nw-" + Guid.NewGuid().ToString("N")[..8];

        // 1. create a customer
        var createCustomer = await admin.PostAsJsonAsync("/customers", new { key, name = "Northwind Traders" });
        Assert.Equal(HttpStatusCode.Created, createCustomer.StatusCode);
        var customer = await createCustomer.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(customer);

        // duplicate key -> 409
        var dup = await admin.PostAsJsonAsync("/customers", new { key, name = "again" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        // 2. add a context
        var addContext = await admin.PostAsJsonAsync(
            $"/customers/{customer!.Id}/contexts", new { name = "Production", kind = "Production" });
        Assert.Equal(HttpStatusCode.Created, addContext.StatusCode);

        // 3. find a target user
        var users = await admin.GetFromJsonAsync<List<UserDto>>("/governance/users");
        var target = Assert.Single(users!, u => u.Email == "gio@globex.example");

        // 4. assign a role at the new customer scope
        var assign = await admin.PostAsJsonAsync(
            $"/governance/users/{target.Id}/assignments",
            new { roleKey = "reader", scopeType = "Customer", customerId = customer.Id, contextId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);
        var assignment = await assign.Content.ReadFromJsonAsync<AssignmentDto>();
        Assert.NotNull(assignment);

        // duplicate assignment -> 409
        var assignDup = await admin.PostAsJsonAsync(
            $"/governance/users/{target.Id}/assignments",
            new { roleKey = "reader", scopeType = "Customer", customerId = customer.Id, contextId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Conflict, assignDup.StatusCode);

        // 5. revoke it
        var revoke = await admin.DeleteAsync(
            $"/governance/users/{target.Id}/assignments/{assignment!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        // revoking again -> 404
        var revokeAgain = await admin.DeleteAsync(
            $"/governance/users/{target.Id}/assignments/{assignment.Id}");
        Assert.Equal(HttpStatusCode.NotFound, revokeAgain.StatusCode);
    }

    private sealed record CustomerDto(Guid Id, string Key, string Name);

    private sealed record UserDto(Guid Id, string Email);

    private sealed record AssignmentDto(Guid Id, Guid UserId, string RoleKey);
}
