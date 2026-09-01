using Iris.Application.Abstractions;

namespace Iris.Api.Tests;

/// <summary>
/// Always-succeeds stand-in for real SMTP, wired into every <see cref="IrisApiFactory"/> instance
/// so API tests never attempt a real network connection — <c>/setup/complete</c> and
/// <c>/setup/test-mail</c> now genuinely try to send mail; the branchy connect/auth/send failure
/// paths themselves are already covered with full control at the handler level
/// (<c>Iris.Application.Tests/Setup/SetupHandlerTests.cs</c>).
/// </summary>
internal sealed class FakeEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task TestConnectionAsync(MailConnectionTestRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
