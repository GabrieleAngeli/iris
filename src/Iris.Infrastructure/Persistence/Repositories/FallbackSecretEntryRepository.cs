using Iris.Application.Abstractions;
using Iris.Domain.Secrets;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class FallbackSecretEntryRepository(IrisDbContext dbContext) : IFallbackSecretEntryRepository
{
    public Task<EncryptedSecretEntry?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        dbContext.Set<EncryptedSecretEntry>().SingleOrDefaultAsync(e => e.Reference == reference, cancellationToken);

    public async Task<IReadOnlyList<EncryptedSecretEntry>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
        await dbContext.Set<EncryptedSecretEntry>()
            .Where(e => e.OwnerUserId == ownerUserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(EncryptedSecretEntry entry, CancellationToken cancellationToken = default) =>
        await dbContext.Set<EncryptedSecretEntry>().AddAsync(entry, cancellationToken).ConfigureAwait(false);
}
