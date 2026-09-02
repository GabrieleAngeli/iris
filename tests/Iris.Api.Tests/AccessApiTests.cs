using System.Net;
using System.Net.Http.Json;

namespace Iris.Api.Tests;

public sealed class AccessApiTests(IrisApiFactory factory) : IClassFixture<IrisApiFactory>
{
    private HttpClient CreateClient(string? devUser = null)
    {
        var client = factory.CreateClient();
        if (devUser is not null)
        {
            client.DefaultRequestHeaders.Add("X-Dev-User", devUser);
        }

        return client;
    }

    [Fact]
    public async Task Health_is_anonymous()
    {
        var response = await CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var response = await CreateClient().GetAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_requires_authentication()
    {
        var response = await CreateClient().GetAsync("/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_as_platform_admin_reports_full_permission_catalog()
    {
        var payload = await CreateClient("admin@iris.local").GetFromJsonAsync<MeDto>("/me");

        Assert.NotNull(payload);
        Assert.Equal("admin@iris.local", payload!.Email);
        Assert.Contains("platform.admin", payload.EffectivePermissions);
        Assert.Contains("governance.roles.manage", payload.EffectivePermissions);
    }

    [Fact]
    public async Task Profile_reports_current_user_permissions_and_access_history()
    {
        var profile = await CreateClient("admin@iris.local").GetFromJsonAsync<ProfileDto>("/profile");

        Assert.NotNull(profile);
        Assert.Equal("admin@iris.local", profile!.Me.Email);
        Assert.Contains("platform.admin", profile.Me.EffectivePermissions);
        var history = Assert.Single(profile.AccessHistory);
        Assert.True(history.IsCurrent);
        Assert.NotEmpty(history.Method);
    }

    [Fact]
    public async Task Customers_are_filtered_to_the_callers_scope()
    {
        var customers = await CreateClient("gio@globex.example").GetFromJsonAsync<List<CustomerDto>>("/customers");

        Assert.NotNull(customers);
        var only = Assert.Single(customers!);
        Assert.Equal("globex", only.Key);
    }

    [Fact]
    public async Task Governance_roles_enforces_the_permission_policy()
    {
        var forbidden = await CreateClient("gio@globex.example").GetAsync("/governance/roles");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var allowed = await CreateClient("admin@iris.local").GetAsync("/governance/roles");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    private sealed record MeDto(string Email, List<string> EffectivePermissions);

    private sealed record ProfileDto(MeDto Me, List<AccessHistoryDto> AccessHistory);

    private sealed record AccessHistoryDto(DateTimeOffset SignedInAtUtc, DateTimeOffset ExpiresAtUtc, string Method, bool IsCurrent);

    private sealed record CustomerDto(string Key, string Name);
}
