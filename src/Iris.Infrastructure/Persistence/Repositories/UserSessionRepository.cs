using Iris.Application.Abstractions;
using Iris.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class UserSessionRepository(IrisDbContext dbContext) : IUserSessionRepository
{
    public async Task AddAsync(UserSession session, CancellationToken cancellationToken = default) =>
        await dbContext.Set<UserSession>().AddAsync(session, cancellationToken).ConfigureAwait(false);

    public Task<UserSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        dbContext.Set<UserSession>().SingleOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

    public void Remove(UserSession session) => dbContext.Set<UserSession>().Remove(session);
}
