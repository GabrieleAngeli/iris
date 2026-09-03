using Iris.Contracts.Applications;

namespace Iris.Extractor.DotNet;

/// <summary>Configuration keys/dependencies found by one scanner, before merging with the others.</summary>
internal sealed record ConfigurationFragment(
    IReadOnlyList<ConfigurationKeyInput> ConfigurationKeys,
    IReadOnlyList<DependencyInput> Dependencies)
{
    public static readonly ConfigurationFragment Empty = new([], []);
}
