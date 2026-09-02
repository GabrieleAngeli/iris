using Iris.Application.Abstractions;
using Iris.Contracts.Audit;
using Iris.Domain.Audit;

namespace Iris.Application.Audit;

public sealed record ListTransactionLogQuery(string? Area, int Take = 50);

public sealed class ListTransactionLogHandler(ITransactionLogRepository logs)
{
    public async Task<IReadOnlyList<TransactionLogEntryResponse>> HandleAsync(
        ListTransactionLogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var take = Math.Clamp(query.Take, 1, 200);
        var area = string.IsNullOrWhiteSpace(query.Area) ? null : query.Area.Trim();
        var entries = await logs.ListAsync(area, take, cancellationToken).ConfigureAwait(false);
        return entries.Select(ToResponse).ToList();
    }

    private static TransactionLogEntryResponse ToResponse(TransactionLogEntry entry) =>
        new(
            entry.Id,
            entry.TransactionId,
            entry.OccurredAtUtc,
            entry.Area,
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            entry.ActorUserId,
            entry.ActorEmail,
            entry.ActorDisplayName,
            entry.ActorExternalId,
            entry.Summary);
}
