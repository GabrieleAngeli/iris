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
        new FakePasswordHasher(),
        new SessionIssuer(store.UserSessionRepository, new FakeClock(Now)),
        new FakeClock(Now),
        store.UnitOfWork);

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

        Assert.False((await StatusHandler(store).HandleAsync(new GetSetupStatusQuery())).NeedsSetup);
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
}
