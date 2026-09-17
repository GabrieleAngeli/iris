using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Iris.Application.Abstractions;

namespace Iris.Infrastructure.Integrations;

/// <summary>
/// Reachability-only connector for Azure DevOps — verifies the configured organization URL and
/// PAT actually work — plus, since this session, a read/write client for the AWX automation
/// repository specifically: reading its blueprint manifest for drift detection
/// (<see cref="IAzureDevOpsRepositoryReader"/>) and proposing changes to it as Pull Requests
/// (<see cref="IAzureDevOpsRepositoryWriter"/>) — Iris never pushes to a base branch directly.
/// </summary>
internal sealed class AzureDevOpsConnector
    : IIntegrationConnector, IAzureDevOpsRepositoryReader, IAzureDevOpsRepositoryWriter, IDisposable
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

    public async Task<AzureDevOpsPullRequestResult> ProposeChangeAsync(
        AzureDevOpsChangeProposal proposal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        // An application-level override (different org, different PAT) wins over the connector's
        // own globally-configured organization/token — see AzureDevOpsChangeProposal's remarks.
        var endpoint = string.IsNullOrWhiteSpace(proposal.Endpoint) ? options.Endpoint : proposal.Endpoint;
        var token = string.IsNullOrWhiteSpace(proposal.Token) ? options.Token : proposal.Token;
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Azure DevOps is not configured (endpoint/token required).");
        }

        var repoBaseUri = $"{Uri.EscapeDataString(proposal.Project)}/_apis/git/repositories/{Uri.EscapeDataString(proposal.Repository)}";

        // 1. Resolve the base branch's current commit — the new branch and its first commit both
        //    build directly on top of it.
        var baseObjectId = await GetBranchObjectIdAsync(endpoint, token, repoBaseUri, proposal.BaseBranch, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Base branch '{proposal.BaseBranch}' not found in {proposal.Project}/{proposal.Repository}.");

        // 2. One push both creates the new branch ref (Azure DevOps creates a ref that doesn't
        //    exist yet, as long as oldObjectId names a real commit to build on) and commits every
        //    file in one changeset. Each file's changeType (add vs edit) depends on whether it
        //    already exists at the base branch tip.
        var changes = new List<object>();
        foreach (var (path, content) in proposal.Files)
        {
            var exists = await FileExistsAsync(endpoint, token, repoBaseUri, proposal.BaseBranch, path, cancellationToken).ConfigureAwait(false);
            changes.Add(new
            {
                changeType = exists ? "edit" : "add",
                item = new { path = NormalizeItemPath(path) },
                newContent = new { content, contentType = "rawtext" },
            });
        }

        var pushBody = new
        {
            refUpdates = new[] { new { name = $"refs/heads/{proposal.NewBranchName}", oldObjectId = baseObjectId } },
            commits = new[] { new { comment = proposal.CommitMessage, changes } },
        };

        using (var pushResponse = await SendAsync(endpoint, token, HttpMethod.Post, $"{repoBaseUri}/pushes?api-version=7.1", pushBody, cancellationToken)
            .ConfigureAwait(false))
        {
            if (!pushResponse.IsSuccessStatusCode)
            {
                var body = await pushResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException($"Azure DevOps rejected the push ({(int)pushResponse.StatusCode}): {body}");
            }
        }

        // 3. Open the PR from the new branch back into the base branch.
        var prBody = new
        {
            sourceRefName = $"refs/heads/{proposal.NewBranchName}",
            targetRefName = $"refs/heads/{proposal.BaseBranch}",
            title = proposal.PrTitle,
            description = proposal.PrDescription,
        };

        using var prResponse = await SendAsync(endpoint, token, HttpMethod.Post, $"{repoBaseUri}/pullrequests?api-version=7.1", prBody, cancellationToken)
            .ConfigureAwait(false);
        var prResponseBody = await prResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!prResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Azure DevOps rejected the pull request ({(int)prResponse.StatusCode}): {prResponseBody}");
        }

        using var prJson = JsonDocument.Parse(prResponseBody);
        var pullRequestId = prJson.RootElement.GetProperty("pullRequestId").GetInt32();
        var url = $"{endpoint.TrimEnd('/')}/{proposal.Project}/_git/{proposal.Repository}/pullrequest/{pullRequestId}";

        return new AzureDevOpsPullRequestResult(pullRequestId, url);
    }

    private async Task<string?> GetBranchObjectIdAsync(string endpoint, string token, string repoBaseUri, string branch, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            endpoint, token, HttpMethod.Get, $"{repoBaseUri}/refs?filter={Uri.EscapeDataString($"heads/{branch}")}&api-version=7.1", null, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Azure DevOps rejected the ref lookup ({(int)response.StatusCode}): {body}");
        }

        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("value", out var value) || value.GetArrayLength() == 0)
        {
            return null;
        }

        return value[0].GetProperty("objectId").GetString();
    }

    private async Task<bool> FileExistsAsync(string endpoint, string token, string repoBaseUri, string branch, string path, CancellationToken cancellationToken)
    {
        var uri = $"{repoBaseUri}/items?path={Uri.EscapeDataString(NormalizeItemPath(path))}" +
            $"&versionDescriptor.version={Uri.EscapeDataString(branch)}&versionDescriptor.versionType=branch&api-version=7.1";
        using var response = await SendAsync(endpoint, token, HttpMethod.Get, uri, null, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private static string NormalizeItemPath(string path) => path.StartsWith('/') ? path : $"/{path}";

    private async Task<HttpResponseMessage> SendAsync(string endpoint, string token, HttpMethod method, string relativeUri, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(new Uri(endpoint.TrimEnd('/') + "/"), relativeUri));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _http.Dispose();
}
