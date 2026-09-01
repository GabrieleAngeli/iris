namespace Iris.Contracts.Governance;

/// <summary>
/// State of the advisory edit lock on a resource. <see cref="Mine"/> is true when the
/// calling operator holds it (they may open their editor); false means someone else does.
/// </summary>
public sealed record EditLockResponse(
    string ResourceType,
    Guid ResourceId,
    bool Mine,
    Guid HolderUserId,
    string HolderDisplayName,
    DateTimeOffset AcquiredAtUtc,
    DateTimeOffset ExpiresAtUtc);
