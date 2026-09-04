using Iris.Application.Abstractions;
using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class ApplicationInstallationRepository(IrisDbContext dbContext) : IApplicationInstallationRepository
{
    public async Task<IReadOnlyList<ApplicationInstallation>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.ApplicationInstallations
            .Include(i => i.Bindings)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<ApplicationInstallation?> GetAsync(Guid installationId, CancellationToken cancellationToken = default) =>
        dbContext.ApplicationInstallations
            .Include(i => i.Bindings)
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == installationId, cancellationToken);

    public async Task AddAsync(ApplicationInstallation installation, CancellationToken cancellationToken = default) =>
        await dbContext.ApplicationInstallations.AddAsync(installation, cancellationToken).ConfigureAwait(false);
}
