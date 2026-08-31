namespace Iris.Domain.Common;

/// <summary>
/// Implemented by entities whose creation and last-modification timestamps are
/// maintained automatically by the persistence layer.
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAtUtc { get; set; }

    DateTimeOffset UpdatedAtUtc { get; set; }
}
