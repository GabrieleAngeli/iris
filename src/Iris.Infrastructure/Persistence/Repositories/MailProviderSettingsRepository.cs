using Iris.Application.Abstractions;
using Iris.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class MailProviderSettingsRepository(IrisDbContext dbContext) : IMailProviderSettingsRepository
{
    public Task<MailProviderSettings?> GetAsync(CancellationToken cancellationToken = default) =>
        dbContext.Set<MailProviderSettings>().SingleOrDefaultAsync(cancellationToken);

    public async Task UpsertAsync(MailProviderSettings settings, CancellationToken cancellationToken = default)
    {
        var set = dbContext.Set<MailProviderSettings>();
        var existing = await set.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            set.Remove(existing);
        }

        await set.AddAsync(settings, cancellationToken).ConfigureAwait(false);
    }
}
