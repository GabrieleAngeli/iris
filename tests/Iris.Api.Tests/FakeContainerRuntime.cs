using Iris.Application.Abstractions;

namespace Iris.Api.Tests;

/// <summary>
/// Stand-in for <see cref="IContainerRuntime"/>, wired into every <see cref="IrisApiFactory"/>
/// instance so API tests never actually shell out to Docker — same rationale as
/// <see cref="FakeEmailSender"/> for SMTP. Registered as a singleton, so it's shared by every
/// test in a class using the same <see cref="IrisApiFactory"/>; xUnit runs facts within one
/// class sequentially by default, so mutating its public properties per-test is safe as long as
/// tests in the same class don't rely on running in parallel.
/// </summary>
internal sealed class FakeContainerRuntime : IContainerRuntime
{
    public bool IsAvailable { get; set; } = true;

    public ContainerStatus Status { get; set; } = new(ContainerState.Absent, null);

    public string Logs { get; set; } =
        "==> OpenBao server started! Log data will stream in below:\nRoot Token: s.fake-root-token\n";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(IsAvailable);

    public Task<ContainerStatus> GetStatusAsync(string containerName, CancellationToken cancellationToken = default) =>
        Task.FromResult(Status);

    public Task<string> RunAsync(ContainerRunSpec spec, CancellationToken cancellationToken = default) =>
        Task.FromResult("fake-container-id");

    public Task<string> GetLogsAsync(string containerName, CancellationToken cancellationToken = default) =>
        Task.FromResult(Logs);
}
