using Iris.Domain.Access;

namespace Iris.Application.Abstractions;

public interface IEditLockRepository
{
    /// <summary>Change-tracked lookup of the lock on a resource, if any.</summary>
    Task<EditLock?> FindAsync(string resourceType, Guid resourceId, CancellationToken cancellationToken = default);

    Task AddAsync(EditLock editLock, CancellationToken cancellationToken = default);

    void Remove(EditLock editLock);
}
