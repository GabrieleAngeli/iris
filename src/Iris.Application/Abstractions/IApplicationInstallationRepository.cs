using Iris.Domain.Applications;

namespace Iris.Application.Abstractions;

public interface IApplicationInstallationRepository
{
    Task<IReadOnlyList<ApplicationInstallation>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ApplicationInstallation?> GetAsync(Guid installationId, CancellationToken cancellationToken = default);

    Task AddAsync(ApplicationInstallation installation, CancellationToken cancellationToken = default);
}
