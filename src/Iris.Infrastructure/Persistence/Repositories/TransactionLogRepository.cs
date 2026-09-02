using Iris.Application.Abstractions;
using Iris.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class TransactionLogRepository(IrisDbContext dbContext) : ITransactionLogRepository
{
    public async Task<IReadOnlyList<TransactionLogEntry>> ListAsync(
        string? area,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<TransactionLogEntry>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(area))
        {
            query = query.Where(e => e.Area == area);
        }

        return (await query
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .Take(take)
            .ToList();
    }
}
