namespace Iris.Application.Abstractions;

/// <summary>
/// Re-reads persisted <c>IntegrationSettings</c> and applies them to the live, in-process
/// connector configuration — called by every <c>Save*IntegrationSettingsHandler</c> right after
/// saving, so a change takes effect immediately instead of requiring an <c>Iris.Api</c> restart.
/// </summary>
public interface IIntegrationSettingsReloader
{
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
