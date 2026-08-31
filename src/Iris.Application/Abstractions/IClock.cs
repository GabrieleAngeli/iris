namespace Iris.Application.Abstractions;

/// <summary>
/// Abstracts wall-clock time so use cases and the validation workflow stay deterministic under test.
/// Implemented in <c>Iris.Infrastructure</c>.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
