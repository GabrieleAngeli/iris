using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Iris.Api.Tests;

public sealed class AuthApiTests(IrisApiFactory factory) : IClassFixture<IrisApiFactory>
{
    private HttpClient Admin()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "admin@iris.local");
        return client;
    }

    /// <summary>A factory that accepts any email, so a freshly pre-provisioned user can sign in.</summary>
    private WebApplicationFactory<Program> AnyEmail() =>
        factory.WithWebHostBuilder(b => b.UseSetting("Iris:Auth:AllowAnyEmail", "true"));

    private static HttpClient As(WebApplicationFactory<Program> f, string email, string? password = null)
    {
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", email);
        if (password is not null)
        {
            client.DefaultRequestHeaders.Add("X-Dev-Password", password);
        }

        return client;
    }

    private async Task<string> PendingUserAsync(string tag)
    {
        var email = $"{tag}-{Guid.NewGuid():N}@contoso.example";
        var create = await Admin().PostAsJsonAsync("/governance/users", new { email, displayName = tag });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return email;
    }

    [Fact]
    public async Task Pre_provisioned_user_is_prompted_for_a_password_then_can_skip()
    {
        var f = AnyEmail();
        var email = await PendingUserAsync("skip");
        var user = As(f, email);

        Assert.True((await user.GetFromJsonAsync<MeDto>("/me"))!.PasswordSetupPending);
        Assert.Equal(HttpStatusCode.NoContent, (await user.PostAsync("/auth/password/skip", null)).StatusCode);
        Assert.False((await user.GetFromJsonAsync<MeDto>("/me"))!.PasswordSetupPending);
    }

    [Fact]
    public async Task Once_a_password_is_set_it_is_required_on_every_dev_sign_in()
    {
        var f = AnyEmail();
        var email = await PendingUserAsync("pw");

        // header-only works until a password exists
        var boot = As(f, email);
        Assert.Equal(HttpStatusCode.NoContent,
            (await boot.PostAsJsonAsync("/auth/password", new { newPassword = "a-good-secret" })).StatusCode);

        // now the bare header is rejected
        Assert.Equal(HttpStatusCode.Unauthorized, (await As(f, email).GetAsync("/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await As(f, email, "wrong").GetAsync("/me")).StatusCode);

        var ok = As(f, email, "a-good-secret");
        var me = await ok.GetFromJsonAsync<MeDto>("/me");
        Assert.False(me!.PasswordSetupPending);
    }

    [Fact]
    public async Task Short_passwords_are_rejected_and_changing_needs_the_current_one()
    {
        var f = AnyEmail();
        var email = await PendingUserAsync("chg");
        var user = As(f, email);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await user.PostAsJsonAsync("/auth/password", new { newPassword = "short" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await user.PostAsJsonAsync("/auth/password", new { newPassword = "first-secret" })).StatusCode);

        var authed = As(f, email, "first-secret");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await authed.PostAsJsonAsync("/auth/password", new { newPassword = "second-secret", currentPassword = "nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await authed.PostAsJsonAsync("/auth/password", new { newPassword = "second-secret", currentPassword = "first-secret" })).StatusCode);
    }

    [Fact]
    public async Task A_seeded_user_without_a_local_password_still_signs_in_with_only_the_header()
    {
        Assert.Equal(HttpStatusCode.OK, (await Admin().GetAsync("/me")).StatusCode);
    }

    [Fact]
    public async Task Password_endpoints_require_authentication()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync("/auth/password", new { newPassword = "a-good-secret" })).StatusCode);
    }

    private sealed record MeDto(Guid UserId, string Email, bool PasswordSetupPending);
}
