using Iris.Domain.Common;

namespace Iris.Domain.Applications;

/// <summary>
/// Compatibility requirement declared by a manifest, such as MongoDB == 6 or Redis >= 6.2 and < 8.
/// It is evaluated later when an installation binds the application to infrastructure or another app.
/// </summary>
public sealed class DependencyConstraintDefinition : Entity<Guid>
{
    private DependencyConstraintDefinition()
        : base(Guid.Empty)
    {
    }

    internal DependencyConstraintDefinition(
        Guid id,
        Guid applicationVersionId,
        string? placeholderKey,
        string? serviceKind,
        string? versionExpression,
        string? detailsJson)
        : base(id)
    {
        ApplicationVersionId = applicationVersionId;
        PlaceholderKey = string.IsNullOrWhiteSpace(placeholderKey) ? null : placeholderKey.Trim();
        ServiceKind = string.IsNullOrWhiteSpace(serviceKind) ? null : serviceKind.Trim();
        VersionExpression = string.IsNullOrWhiteSpace(versionExpression) ? null : versionExpression.Trim();
        DetailsJson = string.IsNullOrWhiteSpace(detailsJson) ? null : detailsJson.Trim();
    }

    public Guid ApplicationVersionId { get; private set; }

    public string? PlaceholderKey { get; private set; }

    public string? ServiceKind { get; private set; }

    public string? VersionExpression { get; private set; }

    public string? DetailsJson { get; private set; }
}
