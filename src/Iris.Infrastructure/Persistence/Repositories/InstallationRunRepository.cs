using Iris.Application.Abstractions;
using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class InstallationRunRepository(IrisDbContext dbContext) : IInstallationRunRepository
{
    public async Task<IReadOnlyList<InstallationRun>> GetForInstallationAsync(
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        // SQLite orders DateTimeOffset lexically; filter in the database, sort in memory
        // (mirrors UserSessionRepository.GetForUserAsync).
        var history = await dbContext.InstallationRuns
            .AsNoTracking()
            .Where(run => run.ApplicationInstallationId == installationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return history.OrderByDescending(run => run.CreatedAtUtc).ToArray();
    }

    public Task<InstallationRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default) =>
        dbContext.InstallationRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(run => run.Id == runId, cancellationToken);

    public Task<InstallationRun?> GetForUpdateAsync(Guid runId, CancellationToken cancellationToken = default) =>
        dbContext.InstallationRuns
            .SingleOrDefaultAsync(run => run.Id == runId, cancellationToken);

    public async Task AddAsync(InstallationRun run, CancellationToken cancellationToken = default) =>
        await dbContext.InstallationRuns.AddAsync(run, cancellationToken).ConfigureAwait(false);
}
