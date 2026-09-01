using Iris.Application.Abstractions;
using Iris.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(IrisDbContext dbContext) : IUserRepository
{
    public Task<User?> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken = default) =>
        dbContext.Users.SingleOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken);

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

    public Task<User?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await dbContext.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);

    public void Remove(User user) => dbContext.Users.Remove(user);
}
