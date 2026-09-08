using Iris.Application.Abstractions;

namespace Iris.Api.Tests;

/// <summary>
/// Stand-in for <see cref="IIntegrationHealthChecker"/>, wired into every
/// <see cref="IrisApiFactory"/> instance — <c>IntegrationHealthCheckBackgroundService</c> runs
/// once immediately on host startup, and the real checker would shell out to real connectors
/// (including a real OS process spawn for Ansible) with no test control over the outcome. Same
/// rationale as <see cref="FakeEmailSender"/>/<see cref="FakeContainerRuntime"/>.
/// </summary>
internal sealed class NoOpIntegrationHealthChecker : IIntegrationHealthChecker
{
    public Task RunOnceAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
