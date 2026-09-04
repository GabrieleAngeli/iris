using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Iris.Application.Abstractions;
using Iris.Application.Common;

namespace Iris.Infrastructure.Integrations;

internal sealed class AwxClient(AwxOptions options) : IAwxClient, IIntegrationConnector, IDisposable
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public string Key => "awx";

    public string Name => "AWX";

    public string? Endpoint => options.Endpoint;

    public async Task<AwxJobLaunchResult> LaunchAsync(
        AwxJobLaunch launch,
        CancellationToken cancellationToken = default)
    {
        if (!options.IsConfigured)
        {
            throw new ValidationException("AWX is not configured. Set endpoint, token and job template id.");
        }

        var jobTemplateId = launch.JobTemplateId ?? options.JobTemplateId;
        if (jobTemplateId is null or <= 0)
        {
            throw new ValidationException("AWX job template id is required.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(options.Endpoint!), $"/api/v2/job_templates/{jobTemplateId}/launch/"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        request.Content = JsonContent.Create(new
        {
            inventory = launch.Package.Inventory,
            limit = launch.Package.Limit,
            job_type = launch.Package.CheckMode ? "check" : null,
            extra_vars = launch.Package.ExtraVars
        });

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ValidationException($"AWX rejected the launch request ({(int)response.StatusCode}): {body}");
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var id = root.TryGetProperty("id", out var idProperty) && idProperty.TryGetInt64(out var parsedId)
            ? parsedId
            : 0;
        var status = root.TryGetProperty("status", out var statusProperty)
            ? statusProperty.GetString() ?? "launched"
            : "launched";
        var url = root.TryGetProperty("url", out var urlProperty)
            ? new Uri(new Uri(options.Endpoint!), urlProperty.GetString() ?? string.Empty).ToString()
            : null;

        return new AwxJobLaunchResult(id, status, url, null);
    }

    public async Task<IntegrationConnectorStatus> GetStatusAsync(
        bool probe = false,
        CancellationToken cancellationToken = default)
    {
        if (!options.IsConfigured)
        {
            return new IntegrationConnectorStatus(Key, Name, "Not configured", Endpoint, "Endpoint, token and job template id are required.");
        }

        if (!probe)
        {
            return new IntegrationConnectorStatus(Key, Name, "Configured", Endpoint, $"Job template: {options.JobTemplateId}");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(options.Endpoint!), "/api/v2/ping/"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
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
