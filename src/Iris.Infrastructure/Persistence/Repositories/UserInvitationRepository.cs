using Iris.Application.Abstractions;
using Iris.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class UserInvitationRepository(IrisDbContext dbContext) : IUserInvitationRepository
{
    public async Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken = default) =>
        await dbContext.Set<UserInvitation>().AddAsync(invitation, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<UserInvitation>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Set<UserInvitation>()
            .Where(i => i.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<UserInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        dbContext.Set<UserInvitation>().SingleOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public void Remove(UserInvitation invitation) => dbContext.Set<UserInvitation>().Remove(invitation);
}
