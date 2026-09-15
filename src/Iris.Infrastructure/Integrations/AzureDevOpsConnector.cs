using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Iris.Application.Abstractions;

namespace Iris.Infrastructure.Integrations;

/// <summary>
/// Reachability-only connector for Azure DevOps — verifies the configured organization URL and
/// PAT actually work, nothing functional beyond that yet (no pipelines/repos/artifacts wired to
/// anything in Iris — requested minimal scope, 2026-09-08).
/// </summary>
internal sealed class AzureDevOpsConnector : IIntegrationConnector, IAzureDevOpsRepositoryReader, IDisposable
{
    private readonly AzureDevOpsOptions options;
    private readonly HttpClient _http;

    public AzureDevOpsConnector(AzureDevOpsOptions options)
        : this(options, new HttpClientHandler())
    {
    }

    /// <summary>Test seam: a stub <see cref="HttpMessageHandler"/> stands in for real HTTP.</summary>
    internal AzureDevOpsConnector(AzureDevOpsOptions options, HttpMessageHandler handler)
    {
        this.options = options;
        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

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

    public async Task<string?> GetFileContentAsync(
        string project,
        string repository,
        string branch,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (string.IsNullOrWhiteSpace(options.Endpoint) || string.IsNullOrWhiteSpace(options.Token))
        {
            throw new InvalidOperationException("Azure DevOps is not configured (endpoint/token required).");
        }

        var uri = new Uri(
            new Uri(options.Endpoint.TrimEnd('/') + "/"),
            $"{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repository)}/items" +
            $"?path={Uri.EscapeDataString(path)}&versionDescriptor.version={Uri.EscapeDataString(branch)}" +
            "&versionDescriptor.versionType=branch&api-version=7.1");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{options.Token}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Azure DevOps rejected the file read ({(int)response.StatusCode}) for {project}/{repository}/{path}@{branch}: {body}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _http.Dispose();
}
