using Iris.Domain.Common;

namespace Iris.Domain.Audit;

public sealed class TransactionLogEntry : Entity<Guid>, IAggregateRoot
{
    private TransactionLogEntry()
        : base(Guid.Empty)
    {
    }

    private TransactionLogEntry(
        Guid id,
        Guid transactionId,
        DateTimeOffset occurredAtUtc,
        string area,
        string action,
        string entityType,
        string entityId,
        Guid? actorUserId,
        string actorEmail,
        string actorDisplayName,
        string? actorExternalId,
        string summary)
        : base(id)
    {
        TransactionId = transactionId;
        OccurredAtUtc = occurredAtUtc;
        Area = Guard(area, nameof(area), 64);
        Action = Guard(action, nameof(action), 32);
        EntityType = Guard(entityType, nameof(entityType), 96);
        EntityId = Guard(entityId, nameof(entityId), 96);
        ActorUserId = actorUserId;
        ActorEmail = Guard(actorEmail, nameof(actorEmail), 256);
        ActorDisplayName = Guard(actorDisplayName, nameof(actorDisplayName), 256);
        ActorExternalId = string.IsNullOrWhiteSpace(actorExternalId) ? null : actorExternalId.Trim();
        Summary = Guard(summary, nameof(summary), 512);
    }

    public Guid TransactionId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string Area { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public Guid? ActorUserId { get; private set; }

    public string ActorEmail { get; private set; } = string.Empty;

    public string ActorDisplayName { get; private set; } = string.Empty;

    public string? ActorExternalId { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public static TransactionLogEntry Record(
        Guid id,
        Guid transactionId,
        DateTimeOffset occurredAtUtc,
        string area,
        string action,
        string entityType,
        string entityId,
        Guid? actorUserId,
        string actorEmail,
        string actorDisplayName,
        string? actorExternalId,
        string summary) =>
        new(
            id,
            transactionId,
            occurredAtUtc,
            area,
            action,
            entityType,
            entityId,
            actorUserId,
            actorEmail,
            actorDisplayName,
            actorExternalId,
            summary);

    private static string Guard(string value, string paramName, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
