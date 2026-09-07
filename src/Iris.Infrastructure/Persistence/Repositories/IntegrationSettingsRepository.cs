using Iris.Application.Abstractions;
using Iris.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class IntegrationSettingsRepository(IrisDbContext dbContext) : IIntegrationSettingsRepository
{
    public Task<IntegrationSettings?> GetAsync(CancellationToken cancellationToken = default) =>
        dbContext.Set<IntegrationSettings>().SingleOrDefaultAsync(cancellationToken);

    public async Task<IntegrationSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var set = dbContext.Set<IntegrationSettings>();
        var existing = await set.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var created = IntegrationSettings.CreateEmpty();
        await set.AddAsync(created, cancellationToken).ConfigureAwait(false);
        return created;
    }
}
