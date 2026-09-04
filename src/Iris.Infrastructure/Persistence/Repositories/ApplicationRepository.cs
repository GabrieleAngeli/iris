using Iris.Application.Abstractions;
using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class ApplicationRepository(IrisDbContext dbContext) : IApplicationRepository
{
    public async Task<IReadOnlyList<ApplicationDefinition>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Applications
            // The summary response counts each version's config keys/dependencies/placeholders
            // (not their content), so the children are still loaded here — just not their own children.
            .Include(a => a.Versions).ThenInclude(v => v.ConfigurationKeys)
            .Include(a => a.Versions).ThenInclude(v => v.Dependencies)
            .Include(a => a.Versions).ThenInclude(v => v.Placeholders)
            .Include(a => a.Versions).ThenInclude(v => v.ApplicationUnits)
            .Include(a => a.Versions).ThenInclude(v => v.InstallationProfiles)
            .Include(a => a.Versions).ThenInclude(v => v.DependencyConstraints)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<ApplicationDefinition?> GetAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
        dbContext.Applications
            .Include(a => a.Versions).ThenInclude(v => v.ConfigurationKeys)
            .Include(a => a.Versions).ThenInclude(v => v.Dependencies)
            .Include(a => a.Versions).ThenInclude(v => v.Placeholders)
            .Include(a => a.Versions).ThenInclude(v => v.ApplicationUnits)
            .Include(a => a.Versions).ThenInclude(v => v.InstallationProfiles)
            .Include(a => a.Versions).ThenInclude(v => v.DependencyConstraints)
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

    public Task<ApplicationDefinition?> GetForUpdateAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
        dbContext.Applications
            .Include(a => a.Versions).ThenInclude(v => v.ConfigurationKeys)
            .Include(a => a.Versions).ThenInclude(v => v.Dependencies)
            .Include(a => a.Versions).ThenInclude(v => v.Placeholders)
            .Include(a => a.Versions).ThenInclude(v => v.ApplicationUnits)
            .Include(a => a.Versions).ThenInclude(v => v.InstallationProfiles)
            .Include(a => a.Versions).ThenInclude(v => v.DependencyConstraints)
            .SingleOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        dbContext.Applications.AnyAsync(a => a.Slug == slug, cancellationToken);

    public async Task AddAsync(ApplicationDefinition application, CancellationToken cancellationToken = default) =>
        await dbContext.Applications.AddAsync(application, cancellationToken).ConfigureAwait(false);
}
