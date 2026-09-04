using Iris.Application.Abstractions;

namespace Iris.Infrastructure.Integrations;

internal sealed class OpenBaoConnector(OpenBaoOptions options) : IIntegrationConnector, IDisposable
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public string Key => "openbao";

    public string Name => "OpenBao";

    public string? Endpoint => options.Endpoint;

    public async Task<IntegrationConnectorStatus> GetStatusAsync(
        bool probe = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return new IntegrationConnectorStatus(Key, Name, "Not configured", null, "Endpoint is required.");
        }

        if (!options.IsSecretStoreConfigured)
        {
            return new IntegrationConnectorStatus(Key, Name, "Configured", Endpoint, "Endpoint configured; token missing, Iris uses the in-memory secret store.");
        }

        if (!probe)
        {
            return new IntegrationConnectorStatus(Key, Name, "Configured", Endpoint, $"Mount: {options.MountPath}");
        }

        try
        {
            using var response = await _http
                .GetAsync(new Uri(new Uri(options.Endpoint), "/v1/sys/health"), cancellationToken)
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
