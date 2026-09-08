using System.Net.Http.Headers;
using System.Text;
using Iris.Application.Abstractions;

namespace Iris.Infrastructure.Integrations;

/// <summary>
/// Reachability-only connector for Azure DevOps — verifies the configured organization URL and
/// PAT actually work, nothing functional beyond that yet (no pipelines/repos/artifacts wired to
/// anything in Iris — requested minimal scope, 2026-09-08).
/// </summary>
internal sealed class AzureDevOpsConnector(AzureDevOpsOptions options) : IIntegrationConnector, IDisposable
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public string Key => "azure-devops";

    public string Name => "Azure DevOps";

    public string? Endpoint => options.Endpoint;

    public async Task<IntegrationConnectorStatus> GetStatusAsync(
        bool probe = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return new IntegrationConnectorStatus(Key, Name, "Not configured", null, "Organization URL is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            return new IntegrationConnectorStatus(Key, Name, "Configured", Endpoint, "Organization URL configured; a personal access token is required to actually reach it.");
        }

        if (!probe)
        {
            return new IntegrationConnectorStatus(Key, Name, "Configured", Endpoint);
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(new Uri(options.Endpoint.TrimEnd('/') + "/"), "_apis/projects?api-version=7.1&$top=1"));
            // Azure DevOps PAT auth: HTTP Basic with an empty username and the PAT as the password.
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{options.Token}")));

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
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
