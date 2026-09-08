using Iris.Application.Common;
using Iris.Application.Settings;
using Iris.Application.Tests.Fakes;
using Iris.Contracts.Setup;

namespace Iris.Application.Tests.Settings;

public sealed class SaveMailProviderSettingsHandlerTests
{
    private static SaveMailProviderSettingsHandler Handler(FakeStore store) =>
        new(store.MailProviderSettingsRepository, store.SecretStore, store.UnitOfWork);

    private static MailProviderInput ValidInput(string? password = "hunter2") =>
        new("smtp.example.com", 587, "no-reply", password, "no-reply@example.com", "Iris", true);

    [Fact]
    public async Task HandleAsync_creates_the_row_and_stores_the_password()
    {
        var store = new FakeStore();

        var result = await Handler(store).HandleAsync(new SaveMailProviderSettingsCommand(ValidInput()));

        Assert.False(result.RestartRequired);
        var settings = Assert.Single(store.MailSettings);
        Assert.Equal("smtp.example.com", settings.SmtpHost);
        Assert.Equal("hunter2", store.SecretsByReference[settings.SmtpPasswordSecretReference!]);
    }

    [Fact]
    public async Task HandleAsync_with_a_blank_password_keeps_the_previously_stored_reference()
    {
        var store = new FakeStore();
        await Handler(store).HandleAsync(new SaveMailProviderSettingsCommand(ValidInput()));
        var firstReference = store.MailSettings.Single().SmtpPasswordSecretReference;

        await Handler(store).HandleAsync(new SaveMailProviderSettingsCommand(ValidInput(password: null) with { FromDisplayName = "Iris Platform" }));

        var settings = Assert.Single(store.MailSettings);
        Assert.Equal("Iris Platform", settings.FromDisplayName);
        Assert.Equal(firstReference, settings.SmtpPasswordSecretReference);
        Assert.Equal("hunter2", store.SecretsByReference[settings.SmtpPasswordSecretReference!]);
    }

    [Theory]
    [InlineData("", 587, "no-reply@example.com")]
    [InlineData("smtp.example.com", 0, "no-reply@example.com")]
    [InlineData("smtp.example.com", 70000, "no-reply@example.com")]
    [InlineData("smtp.example.com", 587, "")]
    public async Task HandleAsync_rejects_invalid_input(string host, int port, string fromAddress)
    {
        var store = new FakeStore();
        var input = new MailProviderInput(host, port, null, "pw", fromAddress, null, true);

        await Assert.ThrowsAsync<ValidationException>(() =>
            Handler(store).HandleAsync(new SaveMailProviderSettingsCommand(input)));
        Assert.Empty(store.MailSettings);
    }
}
