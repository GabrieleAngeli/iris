using Iris.Domain.Applications;

namespace Iris.Application.Abstractions;

public interface IPreparedActionRepository
{
    /// <summary>Every prepared action, for the Actions list (read-only) — filtered/joined in the handler.</summary>
    Task<IReadOnlyList<PreparedAction>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>A single action, not change-tracked.</summary>
    Task<PreparedAction?> GetAsync(Guid actionId, CancellationToken cancellationToken = default);

    /// <summary>A single action, change-tracked for mutation.</summary>
    Task<PreparedAction?> GetForUpdateAsync(Guid actionId, CancellationToken cancellationToken = default);

    Task AddAsync(PreparedAction action, CancellationToken cancellationToken = default);
}
