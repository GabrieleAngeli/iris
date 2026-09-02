using Iris.Domain.Audit;

namespace Iris.Application.Abstractions;

public interface ITransactionLogRepository
{
    Task<IReadOnlyList<TransactionLogEntry>> ListAsync(
        string? area,
        int take,
        CancellationToken cancellationToken = default);
}
