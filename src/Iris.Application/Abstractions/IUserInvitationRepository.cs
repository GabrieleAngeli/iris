using Iris.Domain.Access;

namespace Iris.Application.Abstractions;

public interface IUserInvitationRepository
{
    Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken = default);

    /// <summary>Every invitation currently on file for a user (consumed or not), change-tracked.</summary>
    Task<IReadOnlyList<UserInvitation>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Change-tracked lookup by the hex SHA-256 of a raw token.</summary>
    Task<UserInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    void Remove(UserInvitation invitation);
}
