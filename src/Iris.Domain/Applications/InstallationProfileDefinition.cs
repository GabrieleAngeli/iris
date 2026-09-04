using Iris.Domain.Common;

namespace Iris.Domain.Applications;

/// <summary>
/// A selectable installation profile for a release, for example master/slave variants that need
/// different subsets or defaults for the same configuration key catalog.
/// </summary>
public sealed class InstallationProfileDefinition : Entity<Guid>
{
    private InstallationProfileDefinition()
        : base(Guid.Empty)
    {
        Key = string.Empty;
    }

    internal InstallationProfileDefinition(
        Guid id,
        Guid applicationVersionId,
        string key,
        string? displayName,
        bool required,
        bool multiple,
        string? configurationKeysJson)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        ApplicationVersionId = applicationVersionId;
        Key = key.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        Required = required;
        Multiple = multiple;
        ConfigurationKeysJson = string.IsNullOrWhiteSpace(configurationKeysJson) ? null : configurationKeysJson.Trim();
    }

    public Guid ApplicationVersionId { get; private set; }

    public string Key { get; private set; }

    public string? DisplayName { get; private set; }

    public bool Required { get; private set; }

    public bool Multiple { get; private set; }

    public string? ConfigurationKeysJson { get; private set; }
}
