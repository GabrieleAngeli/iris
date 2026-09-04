using Iris.Domain.Common;

namespace Iris.Domain.Applications;

/// <summary>
/// A launchable unit produced by an application release. A single source/artifact can expose more
/// than one runnable process, such as master, slave, monitor or protocol engine.
/// </summary>
public sealed class ApplicationUnitDefinition : Entity<Guid>
{
    private ApplicationUnitDefinition()
        : base(Guid.Empty)
    {
        Key = string.Empty;
    }

    internal ApplicationUnitDefinition(
        Guid id,
        Guid applicationVersionId,
        string key,
        string? displayName,
        string? kind,
        string? entryPoint,
        string? artifactPath,
        string? executionTargetsJson,
        string? profilesJson)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        ApplicationVersionId = applicationVersionId;
        Key = key.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        Kind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim();
        EntryPoint = string.IsNullOrWhiteSpace(entryPoint) ? null : entryPoint.Trim();
        ArtifactPath = string.IsNullOrWhiteSpace(artifactPath) ? null : artifactPath.Trim();
        ExecutionTargetsJson = string.IsNullOrWhiteSpace(executionTargetsJson) ? null : executionTargetsJson.Trim();
        ProfilesJson = string.IsNullOrWhiteSpace(profilesJson) ? null : profilesJson.Trim();
    }

    public Guid ApplicationVersionId { get; private set; }

    public string Key { get; private set; }

    public string? DisplayName { get; private set; }

    public string? Kind { get; private set; }

    public string? EntryPoint { get; private set; }

    public string? ArtifactPath { get; private set; }

    public string? ExecutionTargetsJson { get; private set; }

    public string? ProfilesJson { get; private set; }
}
