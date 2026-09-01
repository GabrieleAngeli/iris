using Iris.Domain.Applications;

namespace Iris.Application.Abstractions;

public interface IApplicationRepository
{
    /// <summary>
    /// Every application with its versions, including each version's config keys/dependencies/
    /// placeholders — needed for <see cref="ApplicationVersion"/> summary counts — but not those
    /// children's own detail beyond what the summary needs. Read-only.
    /// </summary>
    Task<IReadOnlyList<ApplicationDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>A single application with its versions and every version's full detail, not change-tracked.</summary>
    Task<ApplicationDefinition?> GetAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>A single application with its versions and every version's full detail, change-tracked for mutation.</summary>
    Task<ApplicationDefinition?> GetForUpdateAsync(Guid applicationId, CancellationToken cancellationToken = default);

    Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task AddAsync(ApplicationDefinition application, CancellationToken cancellationToken = default);
}
