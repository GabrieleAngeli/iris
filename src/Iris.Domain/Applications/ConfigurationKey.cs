using Iris.Domain.Common;

namespace Iris.Domain.Applications;

/// <summary>
/// One configuration key an <see cref="ApplicationVersion"/> reads — extracted from its
/// <c>appsettings.json</c>/<c>.env</c>/strongly-typed options/etc, not a value for any particular
/// deployment. <see cref="PlaceholderKey"/> is the suggested domain placeholder binding
/// (e.g. <c>domain.db.main.connectionString</c>) a future deployment would resolve.
/// </summary>
public sealed class ConfigurationKey : Entity<Guid>
{
    // For the persistence layer.
    private ConfigurationKey()
        : base(Guid.Empty)
    {
        Key = string.Empty;
        TargetKind = string.Empty;
    }

    internal ConfigurationKey(
        Guid id,
        Guid applicationVersionId,
        string key,
        string targetKind,
        bool required,
        bool secret,
        string? defaultValue,
        string? description,
        string? purpose,
        string? placeholderKey)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        ApplicationVersionId = applicationVersionId;
        Key = key.Trim();
        TargetKind = targetKind.Trim();
        Required = required;
        Secret = secret;
        DefaultValue = defaultValue;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Purpose = string.IsNullOrWhiteSpace(purpose) ? null : purpose.Trim();
        PlaceholderKey = string.IsNullOrWhiteSpace(placeholderKey) ? null : placeholderKey.Trim();
    }

    public Guid ApplicationVersionId { get; private set; }

    public string Key { get; private set; }

    /// <summary>Where this key lives — e.g. <c>appsettings.json</c>, <c>web.config</c>, <c>env</c>.</summary>
    public string TargetKind { get; private set; }

    public bool Required { get; private set; }

    public bool Secret { get; private set; }

    public string? DefaultValue { get; private set; }

    public string? Description { get; private set; }

    public string? Purpose { get; private set; }

    public string? PlaceholderKey { get; private set; }
}
