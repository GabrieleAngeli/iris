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
    public async Task Admin_can_edit_a_customer_and_reader_cannot()
    {
        var admin = Admin();
        var key = "ed-" + Guid.NewGuid().ToString("N")[..8];

        var create = await admin.PostAsJsonAsync("/customers", new { key, name = "Original Name" });
        var customer = await create.Content.ReadFromJsonAsync<CustomerDto>();

        var forbidden = await Reader().PutAsJsonAsync(
            $"/customers/{customer!.Id}", new { name = "Nope", isActive = true });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var edit = await admin.PutAsJsonAsync(
            $"/customers/{customer.Id}", new { name = "Renamed Corp", isActive = false });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await edit.Content.ReadFromJsonAsync<EditedCustomerDto>();
        Assert.Equal("Renamed Corp", edited!.Name);
        Assert.Equal(key, edited.Key); // unchanged — Key is immutable
        Assert.False(edited.IsActive);

        // unknown customer -> 404
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync(
            $"/customers/{Guid.NewGuid()}", new { name = "X", isActive = true })).StatusCode);
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

    [Fact]
    public async Task Reader_cannot_create_a_user()
    {
        var response = await Reader().PostAsJsonAsync(
            "/governance/users", new { email = "nope@customer.example", displayName = "Nope" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_pre_provision_a_user_and_assign_it_a_role()
    {
        var admin = Admin();
        var email = $"invited-{Guid.NewGuid():N}@customer.example";

        // 1. create the pending user
        var create = await admin.PostAsJsonAsync("/governance/users", new { email, displayName = "Invited Person" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(created);
        Assert.False(created!.IsProvisioned);
        Assert.Empty(created.Assignments);

        // duplicate email -> 409
        var dup = await admin.PostAsJsonAsync("/governance/users", new { email, displayName = "Someone else" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        // it shows up in the list, still pending
        var users = await admin.GetFromJsonAsync<List<UserDto>>("/governance/users");
        var listed = Assert.Single(users!, u => u.Id == created.Id);
        Assert.False(listed.IsProvisioned);

        // 2. assign it a role — the existing assignment endpoint already works on a user
        // that has never signed in, since it only requires the user record to exist
        var assign = await admin.PostAsJsonAsync(
            $"/governance/users/{created.Id}/assignments",
            new { roleKey = "reader", scopeType = "Global", customerId = (Guid?)null, contextId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);
    }

    [Fact]
    public async Task Admin_can_edit_and_delete_a_user()
    {
        var admin = Admin();
        var email = $"edit-{Guid.NewGuid():N}@customer.example";

        var create = await admin.PostAsJsonAsync("/governance/users", new { email, displayName = "Before" });
        var user = await create.Content.ReadFromJsonAsync<UserDto>();
        await admin.PostAsJsonAsync($"/governance/users/{user!.Id}/assignments",
            new { roleKey = "reader", scopeType = "Global", customerId = (Guid?)null, contextId = (Guid?)null });

        // edit profile + active flag
        var newEmail = $"after-{Guid.NewGuid():N}@customer.example";
        var edit = await admin.PutAsJsonAsync($"/governance/users/{user.Id}",
            new { email = newEmail, displayName = "After", isActive = false });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await edit.Content.ReadFromJsonAsync<EditedUserDto>();
        Assert.Equal(newEmail, edited!.Email);
        Assert.Equal("After", edited.DisplayName);
        Assert.False(edited.IsActive);
        Assert.Single(edited.Assignments);

        // reader can't edit or delete
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Reader().PutAsJsonAsync($"/governance/users/{user.Id}", new { email = newEmail, displayName = "x", isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Reader().DeleteAsync($"/governance/users/{user.Id}")).StatusCode);

        // delete
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/governance/users/{user.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/governance/users/{user.Id}")).StatusCode);

        var users = await admin.GetFromJsonAsync<List<UserDto>>("/governance/users");
        Assert.DoesNotContain(users!, u => u.Id == user.Id);
    }

    [Fact]
    public async Task Admin_can_issue_an_invitation_link_and_reader_cannot()
    {
        var admin = Admin();
        var email = $"invite-{Guid.NewGuid():N}@customer.example";
        var create = await admin.PostAsJsonAsync("/governance/users", new { email, displayName = "Invitee" });
        var user = await create.Content.ReadFromJsonAsync<UserDto>();

        var issue = await admin.PostAsync($"/governance/users/{user!.Id}/invitation", null);
        Assert.Equal(HttpStatusCode.OK, issue.StatusCode);
        var invitation = await issue.Content.ReadFromJsonAsync<InvitationDto>();
        Assert.NotNull(invitation);
        Assert.NotEmpty(invitation!.Token);
        Assert.Contains(invitation.Token, invitation.AcceptLink, StringComparison.Ordinal);
        Assert.True(invitation.ExpiresAtUtc > DateTimeOffset.UtcNow);

        // re-issuing gives a different token
        var again = await admin.PostAsync($"/governance/users/{user.Id}/invitation", null);
        var reissued = await again.Content.ReadFromJsonAsync<InvitationDto>();
        Assert.NotEqual(invitation.Token, reissued!.Token);

        // unknown user -> 404
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PostAsync($"/governance/users/{Guid.NewGuid()}/invitation", null)).StatusCode);

        // reader is not allowed
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Reader().PostAsync($"/governance/users/{user.Id}/invitation", null)).StatusCode);
    }

    [Fact]
    public async Task Edit_locks_coordinate_two_operators()
    {
        var admin = Admin();
        var reader = Reader();
        var resource = Guid.NewGuid();

        var mine = await admin.PostAsync($"/locks/user/{resource}", null);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var acquired = await mine.Content.ReadFromJsonAsync<LockDto>();
        Assert.True(acquired!.Mine);

        // the other operator sees it held, and cannot steal it
        var seen = await reader.GetFromJsonAsync<LockDto>($"/locks/user/{resource}");
        Assert.False(seen!.Mine);
        Assert.Equal("Iris Platform Admin", seen.HolderDisplayName);

        var readerTry = await reader.PostAsync($"/locks/user/{resource}", null);
        Assert.False((await readerTry.Content.ReadFromJsonAsync<LockDto>())!.Mine);

        // the other operator cannot release it without force
        Assert.Equal(HttpStatusCode.Conflict, (await reader.DeleteAsync($"/locks/user/{resource}")).StatusCode);

        // the holder releases it and the resource is free again
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/locks/user/{resource}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.GetAsync($"/locks/user/{resource}")).StatusCode);

        // unknown resource type -> 400
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/locks/widget/{resource}", null)).StatusCode);
    }

    private sealed record InvitationDto(
        Guid UserId, string Email, string DisplayName, string Token, string AcceptLink, DateTimeOffset ExpiresAtUtc);

    private sealed record LockDto(string ResourceType, Guid ResourceId, bool Mine, string HolderDisplayName);

    private sealed record CustomerDto(Guid Id, string Key, string Name);

    private sealed record EditedCustomerDto(Guid Id, string Key, string Name, bool IsActive);

    private sealed record UserDto(Guid Id, string Email, bool IsProvisioned, List<object> Assignments);

    private sealed record EditedUserDto(Guid Id, string Email, string DisplayName, bool IsActive, List<object> Assignments);

    private sealed record AssignmentDto(Guid Id, Guid UserId, string RoleKey);
}
