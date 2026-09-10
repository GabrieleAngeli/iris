using System.Net;
using System.Net.Http.Headers;
using Iris.Application.Abstractions;
using Iris.Infrastructure.Integrations;

namespace Iris.Infrastructure.Tests.Integrations;

public sealed class IntegrationReachabilityProbeTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    [Fact]
    public async Task ProbeOpenBao_passes_when_health_and_token_lookup_both_answer_ok()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var probe = new IntegrationReachabilityProbe(handler);

        await probe.ProbeOpenBaoAsync("https://openbao.example:8200", "s.token");

        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/v1/sys/health", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.EndsWith("/v1/auth/token/lookup-self", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.True(handler.Requests[1].Headers.Contains("X-Vault-Token"));
    }

    [Fact]
    public async Task ProbeOpenBao_skips_the_token_lookup_when_no_token_is_given()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var probe = new IntegrationReachabilityProbe(handler);

        await probe.ProbeOpenBaoAsync("https://openbao.example:8200", token: null);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ProbeOpenBao_reports_authenticate_stage_when_the_token_lookup_is_rejected()
    {
        var handler = new StubHandler(request =>
            new HttpResponseMessage(request.RequestUri!.AbsolutePath.EndsWith("lookup-self", StringComparison.Ordinal)
                ? HttpStatusCode.Forbidden
                : HttpStatusCode.OK));
        using var probe = new IntegrationReachabilityProbe(handler);

        var ex = await Assert.ThrowsAsync<IntegrationConnectionException>(
            () => probe.ProbeOpenBaoAsync("https://openbao.example:8200", "bad"));

        Assert.Equal(IntegrationTestStage.Authenticate, ex.Stage);
    }

    [Fact]
    public async Task A_redirect_is_reported_as_a_connect_failure_naming_the_location_instead_of_being_followed()
    {
        var handler = new StubHandler(_ =>
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
            redirect.Headers.Location = new Uri("https://awx.hyperv.internal/api/v2/ping/");
            return redirect;
        });
        using var probe = new IntegrationReachabilityProbe(handler);

        var ex = await Assert.ThrowsAsync<IntegrationConnectionException>(
            () => probe.ProbeAwxAsync("http://awx.hyperv.internal", "token", null, null, null));

        Assert.Equal(IntegrationTestStage.Connect, ex.Stage);
        Assert.Contains("https://awx.hyperv.internal/api/v2/ping/", ex.Message);
        Assert.Single(handler.Requests); // stopped at the first hop, did not follow
    }

    [Fact]
    public async Task ProbeAwx_reports_connect_stage_when_the_host_is_unreachable()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("no route to host"));
        using var probe = new IntegrationReachabilityProbe(handler);

        var ex = await Assert.ThrowsAsync<IntegrationConnectionException>(
            () => probe.ProbeAwxAsync("https://awx.example", "token", null, null, null));

        Assert.Equal(IntegrationTestStage.Connect, ex.Stage);
    }

    [Fact]
    public async Task ProbeAwx_checks_ping_then_me_with_a_bearer_token()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var probe = new IntegrationReachabilityProbe(handler);

        var result = await probe.ProbeAwxAsync("https://awx.example", "awx-token", null, null, null);

        Assert.Equal(new AwxProbeResult(null, null), result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/api/v2/ping/", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.EndsWith("/api/v2/me/", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", handler.Requests[1].Headers.Authorization!.Scheme);
        Assert.Equal("awx-token", handler.Requests[1].Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task ProbeAwx_reports_authenticate_stage_when_me_returns_401()
    {
        var handler = new StubHandler(request =>
            new HttpResponseMessage(request.RequestUri!.AbsolutePath.EndsWith("/me/", StringComparison.Ordinal)
                ? HttpStatusCode.Unauthorized
                : HttpStatusCode.OK));
        using var probe = new IntegrationReachabilityProbe(handler);

        var ex = await Assert.ThrowsAsync<IntegrationConnectionException>(
            () => probe.ProbeAwxAsync("https://awx.example", "expired", null, null, null));

        Assert.Equal(IntegrationTestStage.Authenticate, ex.Stage);
    }

    [Fact]
    public async Task ProbeAwx_with_OAuth_creds_does_the_first_refresh_and_returns_the_rotated_pair()
    {
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/api/o/token/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"new-access","refresh_token":"new-refresh","token_type":"Bearer"}"""),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var probe = new IntegrationReachabilityProbe(handler);

        var result = await probe.ProbeAwxAsync("https://awx.example", null, "client-id", "client-secret", "old-refresh");

        Assert.Equal(new AwxProbeResult("new-access", "new-refresh"), result);
        Assert.Equal(2, handler.Requests.Count); // ping + token, no /me/
        Assert.EndsWith("/api/o/token/", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.Equal("Basic", handler.Requests[1].Headers.Authorization!.Scheme);
    }

    [Fact]
    public async Task ProbeAwx_with_a_public_client_omits_basic_auth_and_puts_client_id_in_the_body()
    {
        string? tokenBody = null;
        AuthenticationHeaderValue? tokenAuth = null;
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/api/o/token/", StringComparison.Ordinal))
            {
                tokenAuth = request.Headers.Authorization;
                tokenBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"a","refresh_token":"b"}"""),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var probe = new IntegrationReachabilityProbe(handler);

        var result = await probe.ProbeAwxAsync("https://awx.example", null, "public-client", null, "old-refresh");

        Assert.Equal(new AwxProbeResult("a", "b"), result);
        Assert.Null(tokenAuth);
        Assert.Contains("client_id=public-client", tokenBody);
    }

    [Fact]
    public async Task ProbeAwx_with_OAuth_creds_reports_authenticate_stage_when_the_refresh_is_rejected()
    {
        var handler = new StubHandler(request =>
            new HttpResponseMessage(request.RequestUri!.AbsolutePath.EndsWith("/api/o/token/", StringComparison.Ordinal)
                ? HttpStatusCode.Unauthorized
                : HttpStatusCode.OK));
        using var probe = new IntegrationReachabilityProbe(handler);

        var ex = await Assert.ThrowsAsync<IntegrationConnectionException>(
            () => probe.ProbeAwxAsync("https://awx.example", null, "client-id", "bad-secret", "old-refresh"));

        Assert.Equal(IntegrationTestStage.Authenticate, ex.Stage);
    }

    [Fact]
    public async Task A_non_http_endpoint_is_rejected_as_a_connect_failure()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var probe = new IntegrationReachabilityProbe(handler);

        var ex = await Assert.ThrowsAsync<IntegrationConnectionException>(
            () => probe.ProbeOpenBaoAsync("not-a-url", null));

        Assert.Equal(IntegrationTestStage.Connect, ex.Stage);
    }
}
