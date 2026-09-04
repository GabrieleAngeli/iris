using Iris.Domain.Applications;

namespace Iris.Application.Abstractions;

public interface IInstallationRunRepository
{
    /// <summary>Runs for one installation, newest first (read-only).</summary>
    Task<IReadOnlyList<InstallationRun>> GetForInstallationAsync(Guid installationId, CancellationToken cancellationToken = default);

    /// <summary>A single run, not change-tracked.</summary>
    Task<InstallationRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>A single run, change-tracked for mutation.</summary>
    Task<InstallationRun?> GetForUpdateAsync(Guid runId, CancellationToken cancellationToken = default);

    Task AddAsync(InstallationRun run, CancellationToken cancellationToken = default);
}
