using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Application.Common;
using Iris.Application.Setup;
using Iris.Application.Tests.Fakes;
using Iris.Contracts.Setup;
using Iris.Domain.Access;

namespace Iris.Application.Tests.Setup;

public sealed class SetupHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static Role PlatformAdminRole() =>
        new(Guid.NewGuid(), "platform-admin", "Platform Administrator", isBuiltIn: true);

    private static GetSetupStatusHandler StatusHandler(FakeStore store) =>
        new(store.RoleRepository, store.RoleAssignmentRepository);

    private static CompleteSetupHandler CompleteHandler(FakeStore store) => new(
        store.RoleRepository,
        store.RoleAssignmentRepository,
        store.UserRepository,
        store.MailProviderSettingsRepository,
        store.SecretStore,
        store.EmailSender,
        new FakePasswordHasher(),
        new SessionIssuer(store.UserSessionRepository, new FakeClock(Now)),
        new FakeClock(Now),
        store.UnitOfWork);

    private static TestMailConnectionHandler TestMailHandler(FakeStore store) => new(store.EmailSender);

    private static MailProviderInput Mail(string? password = "s3cr3t") =>
        new("smtp.example.com", 587, "no-reply", password, "no-reply@example.com", "Iris", true);

    [Fact]
    public async Task NeedsSetup_is_true_until_a_platform_admin_exists()
    {
        var store = new FakeStore();
        var role = PlatformAdminRole();
        store.WithRole(role);

        Assert.True((await StatusHandler(store).HandleAsync(new GetSetupStatusQuery())).NeedsSetup);

        var user = new User(Guid.NewGuid(), "ext-1", "admin@example.com", "Admin");
        store.WithUser(user).WithAssignment(new RoleAssignment(Guid.NewGuid(), user.Id, role.Id, AccessScope.Global()));

        Assert.False((await StatusHandler(store).HandleAsync(new GetSetupStatusQuery())).NeedsSetup);
    }

    [Fact]
    public async Task CompleteSetup_creates_the_admin_role_assignment_session_and_mail_settings()
    {
        var store = new FakeStore();
        store.WithRole(PlatformAdminRole());

        var result = await CompleteHandler(store).HandleAsync(new CompleteSetupCommand(
            Mail(), "admin@example.com", "Root Admin", "a-strong-password"));

        Assert.Equal("admin@example.com", result.Email);
        Assert.NotEmpty(result.Token);

        var user = Assert.Single(store.Users);
        Assert.True(user.HasPassword);
        Assert.True(new FakePasswordHasher().Verify("a-strong-password", user.PasswordHash!));

        var assignment = Assert.Single(store.Assignments);
        Assert.Equal(user.Id, assignment.UserId);

        Assert.Single(store.Sessions);

        var mail = Assert.Single(store.MailSettings);
        Assert.Equal("smtp.example.com", mail.SmtpHost);
        Assert.NotNull(mail.SmtpPasswordSecretReference);
        Assert.Equal("s3cr3t", store.SecretsByReference[mail.SmtpPasswordSecretReference!]);

        // verified for real — as the new admin's own address — before anything was saved
        var tested = Assert.Single(store.EmailSender.TestedConnections);
        Assert.Equal("admin@example.com", tested.TestRecipient);
        Assert.Equal("smtp.example.com", tested.SmtpHost);

        Assert.False((await StatusHandler(store).HandleAsync(new GetSetupStatusQuery())).NeedsSetup);
    }

    [Fact]
    public async Task CompleteSetup_saves_nothing_when_the_mail_test_fails()
    {
        var store = new FakeStore();
        store.WithRole(PlatformAdminRole());
        store.EmailSender.FailTestWith = new MailConnectionException(MailTestStage.Connect, "Could not reach smtp.example.com:587 — timed out");

        await Assert.ThrowsAsync<ValidationException>(() => CompleteHandler(store).HandleAsync(new CompleteSetupCommand(
            Mail(), "admin@example.com", "Root Admin", "a-strong-password")));

        Assert.Empty(store.Users);
        Assert.Empty(store.Assignments);
        Assert.Empty(store.Sessions);
        Assert.Empty(store.MailSettings);
        Assert.Empty(store.SecretsByReference);
        Assert.True((await StatusHandler(store).HandleAsync(new GetSetupStatusQuery())).NeedsSetup);
    }

    [Fact]
    public async Task CompleteSetup_cannot_run_twice()
    {
        var store = new FakeStore();
        store.WithRole(PlatformAdminRole());
        var handler = CompleteHandler(store);

        await handler.HandleAsync(new CompleteSetupCommand(Mail(), "admin@example.com", "Root Admin", "a-strong-password"));

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(new CompleteSetupCommand(
            Mail(), "someone-else@example.com", "Someone Else", "another-password")));
    }

    [Fact]
    public async Task CompleteSetup_validates_mail_and_admin_fields()
    {
        var store = new FakeStore();
        store.WithRole(PlatformAdminRole());
        var handler = CompleteHandler(store);

        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new CompleteSetupCommand(
            new MailProviderInput("", 587, null, null, "from@example.com", null, true),
            "admin@example.com", "Admin", "a-strong-password")));

        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new CompleteSetupCommand(
            new MailProviderInput("smtp.example.com", 0, null, null, "from@example.com", null, true),
            "admin@example.com", "Admin", "a-strong-password")));

        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new CompleteSetupCommand(
            Mail(), "admin@example.com", "Admin", "short")));

        Assert.Empty(store.Users);
    }

    [Fact]
    public async Task TestMailConnection_sends_a_real_test_email_to_the_given_recipient()
    {
        var store = new FakeStore();

        await TestMailHandler(store).HandleAsync(new TestMailConnectionCommand(Mail(), "someone@example.com"));

        var tested = Assert.Single(store.EmailSender.TestedConnections);
        Assert.Equal("someone@example.com", tested.TestRecipient);
        Assert.Equal("smtp.example.com", tested.SmtpHost);
        Assert.Equal(587, tested.SmtpPort);
    }

    [Fact]
    public async Task TestMailConnection_surfaces_a_staged_failure_as_a_validation_error()
    {
        var store = new FakeStore();
        store.EmailSender.FailTestWith = new MailConnectionException(MailTestStage.Authenticate, "Connected, but authentication failed: bad credentials");

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            TestMailHandler(store).HandleAsync(new TestMailConnectionCommand(Mail(), "someone@example.com")));

        Assert.Contains("authentication failed", ex.Message);
    }

    [Fact]
    public async Task TestMailConnection_validates_its_own_fields()
    {
        var store = new FakeStore();
        var handler = TestMailHandler(store);

        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new TestMailConnectionCommand(
            new MailProviderInput("", 587, null, null, "from@example.com", null, true), "someone@example.com")));

        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new TestMailConnectionCommand(
            Mail(), "")));

        Assert.Empty(store.EmailSender.TestedConnections);
    }
}
