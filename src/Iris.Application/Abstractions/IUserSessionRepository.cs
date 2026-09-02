using Iris.Domain.Access;

namespace Iris.Application.Abstractions;

public interface IUserSessionRepository
{
    Task AddAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>Change-tracked lookup by the hex SHA-256 of a raw token.</summary>
    Task<UserSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSession>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    void Remove(UserSession session);
}
