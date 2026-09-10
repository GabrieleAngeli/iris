using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Iris.Application.Abstractions;
using Iris.Application.Common;

namespace Iris.Infrastructure.Integrations;

internal sealed class AwxClient : IAwxClient, IIntegrationConnector, IDisposable
{
    private readonly AwxOptions _options;
    private readonly ISecretStore _secretStore;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SemaphoreSlim _credentialsLock = new(1, 1);

    // Resolved lazily on first use (see EnsureCredentialsAsync): a config/env value if given,
    // otherwise read from ISecretStore by the persisted reference — so a token that only becomes
    // available after an admin unlocks the fallback vault post-startup still works, no second
    // restart. The access + refresh token then change in place after an OAuth2 refresh (AWX
    // rotates the refresh token on every use) and the new pair is written back to ISecretStore.
    private bool _credentialsResolved;
    private string? _accessToken;
    private string? _refreshToken;
    private string? _clientSecret;

    public AwxClient(AwxOptions options, ISecretStore secretStore)
        : this(options, secretStore, new HttpClientHandler())
    {
    }

    /// <summary>Test seam: a stub <see cref="HttpMessageHandler"/> stands in for real HTTP.</summary>
    internal AwxClient(AwxOptions options, ISecretStore secretStore, HttpMessageHandler handler)
    {
        _options = options;
        _secretStore = secretStore;
        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    private async Task EnsureCredentialsAsync(CancellationToken cancellationToken)
    {
        if (_credentialsResolved)
        {
            return;
        }

        await _credentialsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_credentialsResolved)
            {
                return;
            }

            _accessToken = await ResolveAsync(_options.Token, _options.TokenSecretReference, cancellationToken).ConfigureAwait(false);
            _refreshToken = await ResolveAsync(_options.RefreshToken, _options.RefreshTokenSecretReference, cancellationToken).ConfigureAwait(false);
            _clientSecret = await ResolveAsync(_options.OAuthClientSecret, _options.OAuthClientSecretReference, cancellationToken).ConfigureAwait(false);
            _credentialsResolved = true;
        }
        finally
        {
            _credentialsLock.Release();
        }
    }

    private async Task<string?> ResolveAsync(string? value, string? reference, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return string.IsNullOrWhiteSpace(reference)
            ? null
            : await _secretStore.RetrieveAsync(reference, cancellationToken).ConfigureAwait(false);
    }

    public string Key => "awx";

    public string Name => "AWX";

    public string? Endpoint => _options.Endpoint;

    public async Task<AwxJobLaunchResult> LaunchAsync(
        AwxJobLaunch launch,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new ValidationException("AWX is not configured. Set endpoint, token and job template id.");
        }

        var jobTemplateId = launch.JobTemplateId ?? _options.JobTemplateId;
        if (jobTemplateId is null or <= 0)
        {
            throw new ValidationException("AWX job template id is required.");
        }

        await EnsureCredentialsAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            throw new ValidationException(
                "AWX token isn't available yet — unlock the fallback secrets in System settings, or set the token in configuration.");
        }

        var uri = new Uri(new Uri(_options.Endpoint!), $"/api/v2/job_templates/{jobTemplateId}/launch/");
        using var response = await SendWithAuthRetryAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = JsonContent.Create(new
                    {
                        inventory = launch.Package.Inventory,
                        limit = launch.Package.Limit,
                        job_type = launch.Package.CheckMode ? "check" : null,
                        extra_vars = launch.Package.ExtraVars,
                    }),
                };
                return request;
            },
            cancellationToken).ConfigureAwait(false);

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
            ? new Uri(new Uri(_options.Endpoint!), urlProperty.GetString() ?? string.Empty).ToString()
            : null;

        return new AwxJobLaunchResult(id, status, url, null);
    }

    public async Task<AwxJobStatusResult> GetJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new ValidationException("AWX is not configured. Set endpoint, token and job template id.");
        }

        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ValidationException("AWX job id is required.");
        }

        await EnsureCredentialsAsync(cancellationToken).ConfigureAwait(false);

        var uri = new Uri(new Uri(_options.Endpoint!), $"/api/v2/jobs/{jobId}/");
        using var response = await SendWithAuthRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ValidationException($"AWX rejected the job status request ({(int)response.StatusCode}): {body}");
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var status = root.TryGetProperty("status", out var statusProperty)
            ? statusProperty.GetString() ?? "unknown"
            : "unknown";
        var finished = root.TryGetProperty("finished", out var finishedProperty) &&
            finishedProperty.ValueKind is not JsonValueKind.Null;
        var failed = root.TryGetProperty("failed", out var failedProperty) &&
            failedProperty.ValueKind == JsonValueKind.True;
        var url = root.TryGetProperty("url", out var urlProperty)
            ? new Uri(new Uri(_options.Endpoint!), urlProperty.GetString() ?? string.Empty).ToString()
            : null;
        var message = root.TryGetProperty("job_explanation", out var explanationProperty)
            ? explanationProperty.GetString()
            : null;

        return new AwxJobStatusResult(status, finished, finished && !failed, url, message);
    }

    public async Task<IntegrationConnectorStatus> GetStatusAsync(
        bool probe = false,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            return new IntegrationConnectorStatus(Key, Name, "Not configured", Endpoint, "Endpoint, token and job template id are required.");
        }

        var detail = _options.CanRefresh
            ? $"Job template: {_options.JobTemplateId} · OAuth2 auto-renew on"
            : $"Job template: {_options.JobTemplateId}";

        if (!probe)
        {
            return new IntegrationConnectorStatus(Key, Name, "Configured", Endpoint, detail);
        }

        try
        {
            await EnsureCredentialsAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                return new IntegrationConnectorStatus(
                    Key, Name, "Unreachable", Endpoint,
                    "Token isn't available — unlock the fallback secrets in System settings, or set it in configuration.");
            }

            var uri = new Uri(new Uri(_options.Endpoint!), "/api/v2/ping/");
            using var response = await SendWithAuthRetryAsync(
                () => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);
            var status = response.IsSuccessStatusCode ? "Reachable" : "Unreachable";
            return new IntegrationConnectorStatus(Key, Name, status, Endpoint, response.StatusCode.ToString());
        }
        catch (ValidationException ex)
        {
            return new IntegrationConnectorStatus(Key, Name, "Unreachable", Endpoint, ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new IntegrationConnectorStatus(Key, Name, "Unreachable", Endpoint, ex.Message);
        }
    }

    /// <summary>
    /// Sends the request with the current bearer token; on a 401, if the OAuth2 refresh flow is
    /// configured, refreshes the access token once and retries. The caller builds a fresh
    /// <see cref="HttpRequestMessage"/> each attempt (a message — and its content stream — can't
    /// be resent).
    /// </summary>
    private async Task<HttpResponseMessage> SendWithAuthRetryAsync(
        Func<HttpRequestMessage> build,
        CancellationToken cancellationToken)
    {
        var tokenUsed = _accessToken;
        using var first = build();
        first.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        var response = await _http.SendAsync(first, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Unauthorized || !_options.CanRefresh)
        {
            return response;
        }

        response.Dispose();
        await RefreshAsync(tokenUsed, cancellationToken).ConfigureAwait(false);

        using var retry = build();
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await _http.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    private async Task RefreshAsync(string? staleToken, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another concurrent caller already refreshed while we waited for the lock.
            if (!string.Equals(_accessToken, staleToken, StringComparison.Ordinal))
            {
                return;
            }

            using var request = AwxOAuth.BuildRefreshRequest(
                new Uri(new Uri(_options.Endpoint!), "/api/o/token/"),
                _options.OAuthClientId,
                _clientSecret,
                _refreshToken);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new ValidationException(
                    "AWX token refresh failed: " + AwxOAuth.DescribeRefreshFailure((int)response.StatusCode, body));
            }

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var newAccess = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
            var newRefresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
            if (string.IsNullOrWhiteSpace(newAccess))
            {
                throw new ValidationException("AWX token refresh returned no access_token.");
            }

            _accessToken = newAccess;
            await _secretStore.StoreAsync("awx/token", newAccess, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(newRefresh))
            {
                _refreshToken = newRefresh;
                await _secretStore.StoreAsync("awx/refresh-token", newRefresh, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _refreshLock.Dispose();
        _credentialsLock.Dispose();
    }
}
