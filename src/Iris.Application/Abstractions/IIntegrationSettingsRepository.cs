using Iris.Domain.Settings;

namespace Iris.Application.Abstractions;

/// <summary>Single-row settings, like <see cref="IMailProviderSettingsRepository"/>, but
/// mutated in place (via <see cref="IntegrationSettings"/>'s <c>ConfigureXxx</c> methods)
/// rather than replaced whole, since OpenBao/AWX/Ansible are saved independently.</summary>
public interface IIntegrationSettingsRepository
{
    Task<IntegrationSettings?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the existing row, or creates and returns a new empty one (not yet
    /// saved — the caller's own <see cref="IUnitOfWork.SaveChangesAsync"/> persists it
    /// together with whichever <c>ConfigureXxx</c> call the caller makes next).</summary>
    Task<IntegrationSettings> GetOrCreateAsync(CancellationToken cancellationToken = default);
}
