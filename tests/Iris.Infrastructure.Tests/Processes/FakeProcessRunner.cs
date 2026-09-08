using Iris.Infrastructure.Processes;

namespace Iris.Infrastructure.Tests.Processes;

/// <summary>Records every invocation and returns one configurable canned response — every test
/// using this triggers exactly one process call per assertion, so a single response slot (not a
/// per-command dictionary) is enough.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = [];

    public ProcessResult DefaultResponse { get; set; } = new(0, string.Empty, string.Empty);

    public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        Calls.Add((fileName, arguments));
        return Task.FromResult(DefaultResponse);
    }
}
