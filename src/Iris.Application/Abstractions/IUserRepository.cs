using Iris.Domain.Access;

namespace Iris.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    Task<User?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
