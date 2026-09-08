using System.Net;
using System.Net.Http.Json;

namespace Iris.Api.Tests;

/// <summary>
/// End-to-end through the real DI graph (no <c>ISecretStore</c>/vault override in
/// <see cref="IrisApiFactory"/> — the test host boots with no OpenBao configured, so the real
/// <c>EncryptedFallbackSecretStore</c>/<c>FallbackSecretVault</c> are active, same as production
/// before OpenBao is set up). Every test that sets a local password or saves settings uses its
/// own isolated <see cref="IrisApiFactory"/> instance (same pattern as <c>SetupApiTests</c>) —
/// the shared class fixture is only used for the one test that never mutates that state, since
/// xUnit runs facts within a class sequentially but not in a guaranteed declaration order.
/// </summary>
public sealed class FallbackSecretVaultApiTests(IrisApiFactory factory) : IClassFixture<IrisApiFactory>
{
    private static HttpClient Admin(IrisApiFactory f)
    {
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "admin@iris.local");
        return client;
    }

    private static HttpClient Reader(IrisApiFactory f)
    {
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "gio@globex.example");
        return client;
    }

    [Fact]
    public async Task Reader_cannot_unlock_fallback_secrets()
    {
        var response = await Reader(factory).PostAsJsonAsync("/system/settings/secrets/unlock", new { password = "whatever" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_without_a_local_password_is_rejected_with_a_clear_message()
    {
        using var f = new IrisApiFactory();

        var response = await Admin(f).PostAsJsonAsync("/system/settings/secrets/unlock", new { password = "anything" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_with_the_wrong_password_is_rejected()
    {
        using var f = new IrisApiFactory();
        var admin = Admin(f);
        await admin.PostAsJsonAsync("/auth/password", new { newPassword = "correct-horse-battery" });
        // Once a local password exists, the bare X-Dev-User header alone is no longer enough
        // (see AuthApiTests.Once_a_password_is_set_it_is_required_on_every_dev_sign_in) — the
        // dev-header shortcut requires the password too from this point on.
        admin.DefaultRequestHeaders.Add("X-Dev-Password", "correct-horse-battery");

        var response = await admin.PostAsJsonAsync("/system/settings/secrets/unlock", new { password = "wrong-password" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_secret_saved_on_the_fallback_store_survives_an_unlock_and_is_retrievable_in_a_fresh_scope()
    {
        using var f = new IrisApiFactory();
        var admin = Admin(f);
        await admin.PostAsJsonAsync("/auth/password", new { newPassword = "correct-horse-battery" });
        // Once a local password exists, the bare X-Dev-User header alone is no longer enough
        // (see AuthApiTests.Once_a_password_is_set_it_is_required_on_every_dev_sign_in).
        admin.DefaultRequestHeaders.Add("X-Dev-Password", "correct-horse-battery");

        // Saves the SMTP password through the real SaveMailProviderSettingsHandler -> ISecretStore
        // pipeline, landing on the fallback store since no OpenBao is configured in this test host.
        var save = await admin.PutAsJsonAsync("/system/settings/mail", new
        {
            smtpHost = "smtp.example.com",
            smtpPort = 587,
            smtpUsername = "no-reply",
            smtpPassword = "s3cr3t-smtp-password",
            fromAddress = "no-reply@example.com",
            fromDisplayName = "Iris",
            enableSsl = true,
        });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var settingsBefore = await admin.GetFromJsonAsync<SystemSettingsDto>("/system/settings");
        Assert.NotNull(settingsBefore!.FallbackSecrets);
        Assert.True(settingsBefore.FallbackSecrets!.HasPendingWork);

        var unlock = await admin.PostAsJsonAsync("/system/settings/secrets/unlock", new { password = "correct-horse-battery" });
        Assert.Equal(HttpStatusCode.OK, unlock.StatusCode);
        var unlockResult = await unlock.Content.ReadFromJsonAsync<UnlockDto>();
        Assert.True(unlockResult!.Persisted >= 1);

        // Nothing left pending for this same admin once everything they own is durable+in-memory.
        var settingsAfter = await admin.GetFromJsonAsync<SystemSettingsDto>("/system/settings");
        Assert.Null(settingsAfter!.FallbackSecrets);
    }

    private sealed record FallbackSecretsDto(bool HasPendingWork, int PendingPersistCount, int RestorableCount);

    private sealed record SystemSettingsDto(bool CanManageSystem, object? Mail, List<object> Integrations, bool RestartRequired, FallbackSecretsDto? FallbackSecrets);

    private sealed record UnlockDto(int Persisted, int Restored, int OwnershipTransferred, string Message);
}
