using Iris.Application.Abstractions;

namespace Iris.Infrastructure.Integrations;

/// <summary>
/// Reachability-only connector for Nexus Repository Manager — verifies the configured endpoint
/// actually answers, nothing functional beyond that yet (no artifact resolution wired to
/// anything in Iris — requested minimal scope, 2026-09-08). Nexus's own status endpoint is
/// anonymous, so unlike Azure DevOps a token isn't required to check reachability — it's still
/// collected and stored for whenever real artifact operations need it.
/// </summary>
internal sealed class NexusConnector(NexusOptions options) : IIntegrationConnector, IDisposable
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public string Key => "nexus";

    public string Name => "Nexus Repository";

    public string? Endpoint => options.Endpoint;

    public async Task<IntegrationConnectorStatus> GetStatusAsync(
        bool probe = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return new IntegrationConnectorStatus(Key, Name, "Not configured", null, "Endpoint is required.");
        }

        if (!probe)
        {
            return new IntegrationConnectorStatus(Key, Name, "Configured", Endpoint);
        }

        try
        {
            using var response = await _http
                .GetAsync(new Uri(new Uri(options.Endpoint.TrimEnd('/') + "/"), "service/rest/v1/status"), cancellationToken)
                .ConfigureAwait(false);
            var status = response.IsSuccessStatusCode ? "Reachable" : "Unreachable";
            return new IntegrationConnectorStatus(Key, Name, status, Endpoint, response.StatusCode.ToString());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new IntegrationConnectorStatus(Key, Name, "Unreachable", Endpoint, ex.Message);
        }
    }

    public void Dispose() => _http.Dispose();
}
