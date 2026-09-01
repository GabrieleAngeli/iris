using Iris.Domain.Access;

namespace Iris.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive lookup by email. Email has no uniqueness constraint, so this returns the first match.</summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
