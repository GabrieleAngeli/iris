using Iris.Domain.Settings;

namespace Iris.Application.Abstractions;

/// <summary>Single-row settings: <see cref="GetAsync"/> returns <c>null</c> until the setup wizard runs.</summary>
public interface IMailProviderSettingsRepository
{
    Task<MailProviderSettings?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Inserts the (only ever one) row, or replaces it if it already exists.</summary>
    Task UpsertAsync(MailProviderSettings settings, CancellationToken cancellationToken = default);
}
