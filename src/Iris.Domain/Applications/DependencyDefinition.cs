using Iris.Domain.Common;

namespace Iris.Domain.Applications;

/// <summary>
/// An external dependency an <see cref="ApplicationVersion"/> needs (a database, a cache, another
/// service...), extracted from its build artifacts. <see cref="PlaceholderKey"/> is the suggested
/// domain placeholder binding a future deployment would resolve it to.
/// </summary>
public sealed class DependencyDefinition : Entity<Guid>
{
    // For the persistence layer.
    private DependencyDefinition()
        : base(Guid.Empty)
    {
        Name = string.Empty;
        Category = string.Empty;
    }

    internal DependencyDefinition(
        Guid id,
        Guid applicationVersionId,
        string name,
        string category,
        bool required,
        string? description,
        string? placeholderKey)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        ApplicationVersionId = applicationVersionId;
        Name = name.Trim();
        Category = category.Trim();
        Required = required;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        PlaceholderKey = string.IsNullOrWhiteSpace(placeholderKey) ? null : placeholderKey.Trim();
    }

    public Guid ApplicationVersionId { get; private set; }

    public string Name { get; private set; }

    /// <summary>e.g. <c>database</c>, <c>cache</c>, <c>identity</c>, <c>messaging</c>.</summary>
    public string Category { get; private set; }

    public bool Required { get; private set; }

    public string? Description { get; private set; }

    public string? PlaceholderKey { get; private set; }
}
