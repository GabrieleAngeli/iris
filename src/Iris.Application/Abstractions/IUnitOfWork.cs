namespace Iris.Application.Abstractions;

/// <summary>
/// Transactional boundary for a single use case. Repository ports enlist writes;
/// the handler commits once at the end. Implemented in <c>Iris.Infrastructure</c>.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
