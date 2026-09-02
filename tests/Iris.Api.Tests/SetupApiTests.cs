using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Iris.Api.Tests;

public sealed class SetupApiTests(IrisApiFactory factory) : IClassFixture<IrisApiFactory>
{
    [Fact]
    public async Task Default_factory_with_demo_seed_intact_never_needs_setup()
    {
        // Confirms the existing 124 tests' world (demo seed on, admin@iris.local already a
        // platform-admin) is completely undisturbed by this feature.
        var status = await factory.CreateClient().GetFromJsonAsync<StatusDto>("/setup/status");
        Assert.False(status!.NeedsSetup);
    }

    [Fact]
    public async Task Test_mail_connection_succeeds_with_the_fake_sender_and_validates_its_fields()
    {
        var anon = factory.CreateClient();

        var ok = await anon.PostAsJsonAsync("/setup/test-mail", new
        {
            mail = new
            {
                smtpHost = "smtp.example.com",
                smtpPort = 587,
                smtpUsername = "no-reply",
                smtpPassword = "s3cr3t",
                fromAddress = "no-reply@example.com",
                fromDisplayName = "Iris",
                enableSsl = true,
            },
            testRecipient = "someone@example.com",
        });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        var missingRecipient = await anon.PostAsJsonAsync("/setup/test-mail", new
        {
            mail = new
            {
                smtpHost = "smtp.example.com",
                smtpPort = 587,
                smtpUsername = (string?)null,
                smtpPassword = (string?)null,
                fromAddress = "no-reply@example.com",
                fromDisplayName = (string?)null,
                enableSsl = true,
            },
            testRecipient = "",
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingRecipient.StatusCode);
    }

    [Fact]
    public async Task Fresh_install_needs_setup_then_completes_and_cannot_run_twice()
    {
        // A brand new, isolated instance (own temp SQLite file) with demo seeding turned off —
        // the only way to get a genuinely empty database: seeding is otherwise skipped once any
        // user exists, so this must be its own IrisApiFactory, not a variant sharing the class
        // fixture's already-seeded one. `empty` owns disposal (deletes its temp db);
        // `emptyConfigured` is the WithWebHostBuilder variant used to create clients.
        using var empty = new IrisApiFactory(seedDemoData: false);
        WebApplicationFactory<Program> emptyConfigured = empty.WithWebHostBuilder(_ => { });
        var anon = emptyConfigured.CreateClient();

        var before = await anon.GetFromJsonAsync<StatusDto>("/setup/status");
        Assert.True(before!.NeedsSetup);

        var complete = await anon.PostAsJsonAsync("/setup/complete", new
        {
            mail = new
            {
                smtpHost = "smtp.example.com",
                smtpPort = 587,
                smtpUsername = "no-reply",
                smtpPassword = "s3cr3t",
                fromAddress = "no-reply@example.com",
                fromDisplayName = "Iris",
                enableSsl = true,
            },
            adminEmail = "root@example.com",
            adminDisplayName = "Root Admin",
            adminPassword = "a-strong-password",
        });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var result = await complete.Content.ReadFromJsonAsync<CompleteSetupDto>();
        Assert.Equal("root@example.com", result!.Email);
        Assert.NotEmpty(result.Token);

        // the returned session token signs the new admin straight in
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Token);
        var me = await anon.GetFromJsonAsync<MeDto>("/me");
        Assert.Equal("root@example.com", me!.Email);

        // setup is done — status now says so, and a replay is rejected
        var after = await emptyConfigured.CreateClient().GetFromJsonAsync<StatusDto>("/setup/status");
        Assert.False(after!.NeedsSetup);

        var replay = await emptyConfigured.CreateClient().PostAsJsonAsync("/setup/complete", new
        {
            mail = new
            {
                smtpHost = "smtp.example.com",
                smtpPort = 587,
                smtpUsername = (string?)null,
                smtpPassword = (string?)null,
                fromAddress = "no-reply@example.com",
                fromDisplayName = (string?)null,
                enableSsl = false,
            },
            adminEmail = "someone-else@example.com",
            adminDisplayName = "Someone Else",
            adminPassword = "another-password",
        });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }

    [Fact]
    public async Task Allow_listed_authenticated_user_can_claim_the_first_platform_admin_role()
    {
        using var empty = new IrisApiFactory(seedDemoData: false);
        WebApplicationFactory<Program> emptyConfigured =
            empty.WithWebHostBuilder(b =>
            {
                b.UseSetting("Iris:Setup:AdminClaimEmails:0", "admin@iris.local");
            });

        var client = emptyConfigured.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "admin@iris.local");

        var before = await emptyConfigured.CreateClient().GetFromJsonAsync<StatusDto>("/setup/status");
        Assert.True(before!.NeedsSetup);

        var claim = await client.PostAsync("/setup/claim-admin", content: null);
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        var result = await claim.Content.ReadFromJsonAsync<ClaimSetupAdminDto>();
        Assert.Equal("admin@iris.local", result!.Email);

        var me = await client.GetFromJsonAsync<MeDto>("/me");
        Assert.Contains("platform.admin", me!.EffectivePermissions);

        var after = await emptyConfigured.CreateClient().GetFromJsonAsync<StatusDto>("/setup/status");
        Assert.False(after!.NeedsSetup);
    }

    [Fact]
    public async Task Authenticated_user_outside_the_claim_allow_list_cannot_claim_setup_admin()
    {
        using var empty = new IrisApiFactory(seedDemoData: false);
        WebApplicationFactory<Program> emptyConfigured =
            empty.WithWebHostBuilder(b =>
            {
                b.UseSetting("Iris:Setup:AdminClaimEmails:0", "admin@iris.local");
            });

        var client = emptyConfigured.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "gio@globex.example");

        var claim = await client.PostAsync("/setup/claim-admin", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, claim.StatusCode);

        var status = await emptyConfigured.CreateClient().GetFromJsonAsync<StatusDto>("/setup/status");
        Assert.True(status!.NeedsSetup);
    }

    private sealed record StatusDto(bool NeedsSetup);

    private sealed record CompleteSetupDto(Guid UserId, string Email, string Token, DateTimeOffset ExpiresAtUtc);

    private sealed record ClaimSetupAdminDto(Guid UserId, string Email, string DisplayName);

    private sealed record MeDto(Guid UserId, string Email, IReadOnlyList<string> EffectivePermissions);
}
