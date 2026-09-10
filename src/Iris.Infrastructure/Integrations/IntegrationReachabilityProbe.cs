using System.Net.Http.Headers;
using System.Text.Json;
using Iris.Application.Abstractions;

namespace Iris.Infrastructure.Integrations;

/// <summary>
/// Real HTTP checks against an OpenBao/AWX endpoint + token that may not be persisted yet. Same
/// role as <c>SmtpEmailSender.TestConnectionAsync</c> for SMTP. Every request is bounded by a
/// short per-call timeout, does <b>not</b> follow redirects (a 3xx is reported as an actionable
/// error — a followed redirect that changes scheme silently drops the auth header) and does
/// <b>not</b> go through a system/web proxy (these are internal service hostnames).
/// </summary>
internal sealed class IntegrationReachabilityProbe : IIntegrationReachabilityProbe, IDisposable
{
    private static readonly TimeSpan PerRequestTimeout = TimeSpan.FromSeconds(6);

    private readonly HttpClient _http;

    public IntegrationReachabilityProbe()
        : this(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
        })
    {
    }

    /// <summary>Test seam: lets a stub <see cref="HttpMessageHandler"/> stand in for real HTTP.</summary>
    internal IntegrationReachabilityProbe(HttpMessageHandler handler)
    {
        _http = new HttpClient(handler)
        {
            // Outer bound only — each call also has its own PerRequestTimeout CTS below.
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    public async Task ProbeOpenBaoAsync(string endpoint, string? token, CancellationToken cancellationToken = default)
    {
        var baseUri = ParseEndpoint(endpoint, "OpenBao");

        // Any HTTP status back means OpenBao is answering: 200 active, 429 standby, 472/473
        // DR/perf-standby, 501 not-initialised, 503 sealed — all "it's there".
        (await SendAsync("OpenBao", () => new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "v1/sys/health")), cancellationToken)
            .ConfigureAwait(false)).Dispose();

        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        using var response = await SendAsync("OpenBao", () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "v1/auth/token/lookup-self"));
            request.Headers.TryAddWithoutValidation("X-Vault-Token", token);
            return request;
        }, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new IntegrationConnectionException(
                IntegrationTestStage.Authenticate,
                $"OpenBao rejected the token ({(int)response.StatusCode}).");
        }
    }

    public async Task<AwxProbeResult> ProbeAwxAsync(
        string endpoint,
        string? token,
        string? oAuthClientId,
        string? oAuthClientSecret,
        string? refreshToken,
        CancellationToken cancellationToken = default)
    {
        var baseUri = ParseEndpoint(endpoint, "AWX");

        (await SendAsync("AWX", () => new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "api/v2/ping/")), cancellationToken)
            .ConfigureAwait(false)).Dispose();

        var hasOAuth = !string.IsNullOrWhiteSpace(oAuthClientId) &&
            !string.IsNullOrWhiteSpace(refreshToken);

        if (hasOAuth)
        {
            // A successful refresh proves endpoint + client credentials + refresh token in one
            // shot. AWX rotates the refresh token, so hand the fresh pair back to be persisted.
            using var response = await SendAsync(
                "AWX",
                () => AwxOAuth.BuildRefreshRequest(
                    new Uri(baseUri, "api/o/token/"), oAuthClientId, oAuthClientSecret, refreshToken),
                cancellationToken).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new IntegrationConnectionException(
                    IntegrationTestStage.Authenticate,
                    AwxOAuth.DescribeRefreshFailure((int)response.StatusCode, body));
            }

            try
            {
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;
                var access = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
                var rotated = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
                if (string.IsNullOrWhiteSpace(access))
                {
                    throw new IntegrationConnectionException(
                        IntegrationTestStage.Authenticate, "AWX token refresh returned no access_token.");
                }

                return new AwxProbeResult(access, string.IsNullOrWhiteSpace(rotated) ? refreshToken : rotated);
            }
            catch (JsonException)
            {
                throw new IntegrationConnectionException(
                    IntegrationTestStage.Authenticate, "AWX token refresh response was not valid JSON.");
            }
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            using var response = await SendAsync("AWX", () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "api/v2/me/"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return request;
            }, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new IntegrationConnectionException(
                    IntegrationTestStage.Authenticate,
                    $"AWX rejected the token ({(int)response.StatusCode}).");
            }
        }

        return new AwxProbeResult(null, null);
    }

    private static Uri ParseEndpoint(string endpoint, string service)
    {
        if (!Uri.TryCreate(EnsureTrailingSlash(endpoint), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new IntegrationConnectionException(
                IntegrationTestStage.Connect,
                $"'{endpoint}' is not a valid {service} URL (expected http:// or https://).");
        }

        return uri;
    }

    private static string EnsureTrailingSlash(string endpoint) =>
        endpoint.EndsWith('/') ? endpoint : endpoint + "/";

    private async Task<HttpResponseMessage> SendAsync(
        string service,
        Func<HttpRequestMessage> build,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PerRequestTimeout);

        using var request = build();
        var uri = request.RequestUri!;

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // genuine caller cancellation (client disconnected) — not our timeout
        }
        catch (OperationCanceledException)
        {
            throw new IntegrationConnectionException(
                IntegrationTestStage.Connect,
                $"{service} at {uri.GetLeftPart(UriPartial.Authority)} did not respond within {PerRequestTimeout.TotalSeconds:0}s.");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or UriFormatException)
        {
            throw new IntegrationConnectionException(
                IntegrationTestStage.Connect,
                $"Could not reach {service} at {uri.GetLeftPart(UriPartial.Authority)}: {ex.Message}");
        }

        // A redirect is followed nowhere on purpose: if it changed scheme/host the auth header
        // would be silently dropped and the operator would see a misleading "rejected the token".
        var statusCode = (int)response.StatusCode;
        if (statusCode is >= 300 and < 400)
        {
            var location = response.Headers.Location?.ToString();
            response.Dispose();
            throw new IntegrationConnectionException(
                IntegrationTestStage.Connect,
                location is null
                    ? $"{service} at {uri} returned a redirect ({statusCode}); use the final URL as the endpoint."
                    : $"{service} redirects {uri} -> {location}; use that URL as the endpoint instead.");
        }

        return response;
    }

    public void Dispose() => _http.Dispose();
}
