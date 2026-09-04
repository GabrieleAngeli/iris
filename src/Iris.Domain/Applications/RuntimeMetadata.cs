using Iris.Domain.Infrastructure;

namespace Iris.Domain.Applications;

/// <summary>
/// What an <see cref="ApplicationVersion"/> needs from the machine it runs on — captured now so
/// a future deployment validation engine can compare it against a <see cref="ServerNode"/>. Owned
/// by its <see cref="ApplicationVersion"/>: no identity of its own, replaced wholesale on re-import.
/// </summary>
public sealed class RuntimeMetadata
{
    // For the persistence layer.
    private RuntimeMetadata()
    {
        RuntimeName = string.Empty;
        RequiredPorts = [];
    }

    public RuntimeMetadata(
        string runtimeName,
        ServerOs? preferredOs,
        int? requiredCpuCores,
        int? requiredMemoryMb,
        IEnumerable<int>? requiredPorts,
        string? executionTargetsJson = null,
        string? osSupportJson = null,
        int? minimumCpuCores = null,
        int? minimumMemoryMb = null,
        string? portKeysJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeName);

        RuntimeName = runtimeName.Trim();
        PreferredOs = preferredOs;
        RequiredCpuCores = requiredCpuCores;
        RequiredMemoryMb = requiredMemoryMb;
        RequiredPorts = requiredPorts?.Distinct().Order().ToList() ?? [];
        ExecutionTargetsJson = string.IsNullOrWhiteSpace(executionTargetsJson) ? null : executionTargetsJson.Trim();
        OsSupportJson = string.IsNullOrWhiteSpace(osSupportJson) ? null : osSupportJson.Trim();
        MinimumCpuCores = minimumCpuCores;
        MinimumMemoryMb = minimumMemoryMb;
        PortKeysJson = string.IsNullOrWhiteSpace(portKeysJson) ? null : portKeysJson.Trim();
    }

    public string RuntimeName { get; private set; }

    /// <summary>Reuses <see cref="Iris.Domain.Infrastructure.ServerOs"/> rather than a duplicate enum.</summary>
    public ServerOs? PreferredOs { get; private set; }

    public int? RequiredCpuCores { get; private set; }

    public int? RequiredMemoryMb { get; private set; }

    /// <summary>
    /// A plain scalar collection, not a navigation to related entities (unlike e.g.
    /// <c>ServerNode.Credentials</c>) — a normal private-set property is enough, EF maps it as a
    /// native primitive collection.
    /// </summary>
    public IReadOnlyList<int> RequiredPorts { get; private set; }

    public string? ExecutionTargetsJson { get; private set; }

    public string? OsSupportJson { get; private set; }

    public int? MinimumCpuCores { get; private set; }

    public int? MinimumMemoryMb { get; private set; }

    public string? PortKeysJson { get; private set; }
}
