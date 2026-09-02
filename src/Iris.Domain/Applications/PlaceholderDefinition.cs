using Iris.Domain.Common;

namespace Iris.Domain.Applications;

/// <summary>
/// A domain placeholder an <see cref="ApplicationVersion"/> exposes (e.g.
/// <c>domain.db.main.connectionString</c>) — the seam between what the application requires and
/// what infrastructure a future deployment resolves it to.
/// </summary>
public sealed class PlaceholderDefinition : Entity<Guid>
{
    // For the persistence layer.
    private PlaceholderDefinition()
        : base(Guid.Empty)
    {
        Key = string.Empty;
    }

    internal PlaceholderDefinition(
        Guid id,
        Guid applicationVersionId,
        string key,
        string? category,
        string? description,
        bool required)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        ApplicationVersionId = applicationVersionId;
        Key = key.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Required = required;
    }

    public Guid ApplicationVersionId { get; private set; }

    public string Key { get; private set; }

    public string? Category { get; private set; }

    public string? Description { get; private set; }

    public bool Required { get; private set; }
}
