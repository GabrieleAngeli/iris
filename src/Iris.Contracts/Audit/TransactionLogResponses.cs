namespace Iris.Contracts.Audit;

public sealed record TransactionLogEntryResponse(
    Guid Id,
    Guid TransactionId,
    DateTimeOffset OccurredAtUtc,
    string Area,
    string Action,
    string EntityType,
    string EntityId,
    Guid? ActorUserId,
    string ActorEmail,
    string ActorDisplayName,
    string? ActorExternalId,
    string Summary);
