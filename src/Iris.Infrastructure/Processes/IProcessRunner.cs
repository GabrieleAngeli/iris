namespace Iris.Infrastructure.Processes;

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Seam between <c>DockerCliContainerRuntime</c> and <see cref="System.Diagnostics.Process"/>,
/// purely so the command-construction/output-parsing logic can be unit tested with a fake
/// instead of needing a real Docker daemon in every test run.
/// </summary>
internal interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
