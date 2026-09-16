using System.Net;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Infrastructure.Integrations;

namespace Iris.Infrastructure.Tests.Integrations;

public sealed class AwxClientTests
{
    private static readonly AnsibleExecutionPackage Package =
        new("iris-deploy.yml", "iris-dev", null, false, new Dictionary<string, object?>());

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Method, string Path, string? AuthScheme)> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request.Method.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.Scheme));
            return Task.FromResult(respond(request));
        }
    }

    private sealed class RecordingSecretStore : ISecretStore
    {
        /// <summary>Written values, keyed by logical path — for assertions.</summary>
        public Dictionary<string, string> Stored { get; } = [];

        private readonly Dictionary<string, string> _byReference = [];

        public void Seed(string reference, string value) => _byReference[reference] = value;

        public Task<string> StoreAsync(string logicalPath, string secretValue, CancellationToken cancellationToken = default)
        {
            Stored[logicalPath] = secretValue;
            var reference = $"ref://{logicalPath}";
            _byReference[reference] = secretValue;
            return Task.FromResult(reference);
        }

        public Task<string?> RetrieveAsync(string reference, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byReference.GetValueOrDefault(reference));

        public Task DeleteAsync(string reference, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static AwxOptions RefreshableOptions() => new()
    {
        Endpoint = "https://awx.example",
        Token = "old-access",
        JobTemplateId = 7,
        OAuthClientId = "client-id",
        OAuthClientSecret = "client-secret",
        RefreshToken = "old-refresh",
    };

    private static AwxOptions PublicClientOptions() => new()
    {
        Endpoint = "https://awx.example",
        Token = "old-access",
        JobTemplateId = 7,
        OAuthClientId = "client-id",
        OAuthClientSecret = null, // Public application — no secret
        RefreshToken = "old-refresh",
    };

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body) };

    [Fact]
    public async Task Launch_refreshes_the_token_on_a_401_then_retries_and_persists_the_rotated_pair()
    {
        var launchAttempts = 0;
        var handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/o/token/", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"access_token":"new-access","refresh_token":"new-refresh"}""");
            }

            // First launch attempt is unauthorized, the retry (after refresh) succeeds.
            return ++launchAttempts == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Json(HttpStatusCode.OK, """{"id":99,"status":"pending"}""");
        });
        var secrets = new RecordingSecretStore();
        using var client = new AwxClient(RefreshableOptions(), secrets, handler);

        var result = await client.LaunchAsync(new AwxJobLaunch(7, Package));

        Assert.Equal(99, result.JobId);
        Assert.Equal(2, launchAttempts);
        Assert.Contains(handler.Calls, c => c.Path.EndsWith("/api/o/token/", StringComparison.Ordinal) && c.AuthScheme == "Basic");
        Assert.Equal("new-access", secrets.Stored["awx/token"]);
        Assert.Equal("new-refresh", secrets.Stored["awx/refresh-token"]);
    }

    [Fact]
    public async Task Launch_refreshes_a_public_client_by_sending_client_id_in_the_body_and_no_basic_header()
    {
        var launchAttempts = 0;
        string? tokenRequestBody = null;
        var handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/o/token/", StringComparison.Ordinal))
            {
                Assert.Null(request.Headers.Authorization); // no Basic header for a Public client
                tokenRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return Json(HttpStatusCode.OK, """{"access_token":"new-access","refresh_token":"new-refresh"}""");
            }

            return ++launchAttempts == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Json(HttpStatusCode.OK, """{"id":1,"status":"pending"}""");
        });
        var secrets = new RecordingSecretStore();
        using var client = new AwxClient(PublicClientOptions(), secrets, handler);

        await client.LaunchAsync(new AwxJobLaunch(7, Package));

        Assert.Contains("client_id=client-id", tokenRequestBody);
        Assert.Contains("grant_type=refresh_token", tokenRequestBody);
        Assert.Equal("new-refresh", secrets.Stored["awx/refresh-token"]);
    }

    [Fact]
    public async Task Resolves_the_token_lazily_from_the_secret_store_when_only_a_reference_is_configured()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"id":5,"status":"pending"}"""));
        var secrets = new RecordingSecretStore();
        secrets.Seed("vault://awx-token", "resolved-access");
        using var client = new AwxClient(
            new AwxOptions { Endpoint = "https://awx.example", TokenSecretReference = "vault://awx-token", JobTemplateId = 7 },
            secrets,
            handler);

        // Reaching the API (200) only happens once the bearer token has been resolved from the
        // store — an unresolved token throws before any HTTP call (see the next test).
        var result = await client.LaunchAsync(new AwxJobLaunch(7, Package));

        Assert.Equal(5, result.JobId);
    }

    [Fact]
    public async Task Launch_fails_clearly_when_the_token_reference_cannot_be_resolved_yet()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new AwxClient(
            new AwxOptions { Endpoint = "https://awx.example", TokenSecretReference = "vault://not-unlocked-yet", JobTemplateId = 7 },
            new RecordingSecretStore(), // nothing seeded — simulates a locked fallback vault
            handler);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => client.LaunchAsync(new AwxJobLaunch(7, Package)));

        Assert.Contains("unlock", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Calls); // never hit the network
    }

    [Fact]
    public async Task Launch_does_not_refresh_when_the_OAuth_flow_is_not_configured()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = new AwxClient(
            new AwxOptions { Endpoint = "https://awx.example", Token = "static", JobTemplateId = 7 },
            new RecordingSecretStore(),
            handler);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => client.LaunchAsync(new AwxJobLaunch(7, Package)));

        Assert.Contains("401", ex.Message);
        Assert.DoesNotContain(handler.Calls, c => c.Path.EndsWith("/api/o/token/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failing_refresh_surfaces_as_a_validation_error()
    {
        var handler = new StubHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/api/o/token/", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = new AwxClient(RefreshableOptions(), new RecordingSecretStore(), handler);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => client.LaunchAsync(new AwxJobLaunch(7, Package)));

        Assert.Contains("refresh failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHostFacts_scopes_the_host_lookup_to_the_job_templates_own_inventory()
    {
        var handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/v2/job_templates/7/", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"id":7,"inventory":55}""");
            }

            if (path.StartsWith("/api/v2/inventories/55/hosts/", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"results":[{"id":9}]}""");
            }

            if (path.EndsWith("/api/v2/hosts/9/ansible_facts/", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"ansible_memtotal_mb":8192}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        using var client = new AwxClient(
            new AwxOptions { Endpoint = "https://awx.example", Token = "static", JobTemplateId = 7 },
            new RecordingSecretStore(),
            handler);

        var result = await client.GetHostFactsAsync(7, "web");

        Assert.True(result.HostFound);
        Assert.True(result.Facts!["ansible_memtotal_mb"].GetInt32() == 8192);
        // The host lookup must be scoped to inventory 55 (the job template's own), never a
        // global /api/v2/hosts/?name= search — the same alias ("web") can legitimately exist
        // in many different customers' inventories.
        Assert.Contains(handler.Calls, c => c.Path.StartsWith("/api/v2/inventories/55/hosts/", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Calls, c => c.Path == "/api/v2/hosts/" || c.Path.StartsWith("/api/v2/hosts/?", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetHostFacts_reports_not_found_when_the_job_templates_inventory_has_no_such_host()
    {
        var handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/v2/job_templates/7/", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"id":7,"inventory":55}""");
            }

            return Json(HttpStatusCode.OK, """{"results":[]}""");
        });
        using var client = new AwxClient(
            new AwxOptions { Endpoint = "https://awx.example", Token = "static", JobTemplateId = 7 },
            new RecordingSecretStore(),
            handler);

        var result = await client.GetHostFactsAsync(7, "web");

        Assert.False(result.HostFound);
    }

    [Fact]
    public async Task GetJobTemplate_returns_the_playbook_and_use_fact_cache_flag()
    {
        var handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            return path.EndsWith("/api/v2/job_templates/", StringComparison.Ordinal)
                ? Json(HttpStatusCode.OK, """{"results":[{"id":12,"playbook":"playbooks/validation/discover-host-facts.yml","use_fact_cache":true}]}""")
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        using var client = new AwxClient(
            new AwxOptions { Endpoint = "https://awx.example", Token = "static", JobTemplateId = 7 },
            new RecordingSecretStore(),
            handler);

        var result = await client.GetJobTemplateAsync("cloud_02-trial-facts");

        Assert.NotNull(result);
        Assert.Equal(12, result!.Id);
        Assert.Equal("playbooks/validation/discover-host-facts.yml", result.Playbook);
        Assert.True(result.UseFactCache);
    }

    [Fact]
    public async Task GetJobTemplate_returns_null_when_no_template_has_that_name()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"results":[]}"""));
        using var client = new AwxClient(
            new AwxOptions { Endpoint = "https://awx.example", Token = "static", JobTemplateId = 7 },
            new RecordingSecretStore(),
            handler);

        var result = await client.GetJobTemplateAsync("does-not-exist");

        Assert.Null(result);
    }

    [Fact]
    public async Task Concurrent_401s_trigger_only_one_refresh()
    {
        var tokenCalls = 0;
        var handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/o/token/", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref tokenCalls);
                Thread.Sleep(20); // widen the race window
                return Json(HttpStatusCode.OK, """{"access_token":"new-access","refresh_token":"new-refresh"}""");
            }

            // Anything still presenting the old token is unauthorized; the refreshed one passes.
            return request.Headers.Authorization?.Parameter == "old-access"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Json(HttpStatusCode.OK, """{"status":"successful","finished":"2026-01-01T00:00:00Z"}""");
        });
        using var client = new AwxClient(RefreshableOptions(), new RecordingSecretStore(), handler);

        await Task.WhenAll(
            client.GetJobStatusAsync("1"),
            client.GetJobStatusAsync("2"),
            client.GetJobStatusAsync("3"));

        Assert.Equal(1, tokenCalls);
    }

    [Fact]
    public async Task GetJobStatus_reads_the_elapsed_field()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"status":"successful","finished":"2026-01-01T00:00:00Z","failed":false,"elapsed":12.345}"""));
        using var client = new AwxClient(
            new AwxOptions { Endpoint = "https://awx.example", Token = "static", JobTemplateId = 7 },
            new RecordingSecretStore(),
            handler);

        var result = await client.GetJobStatusAsync("42");

        Assert.Equal(12.345, result.ElapsedSeconds);
    }

    [Fact]
    public async Task GetJobStatus_leaves_elapsed_null_when_the_field_is_absent()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"status":"pending","finished":null,"failed":false}"""));
        using var client = new AwxClient(
            new AwxOptions { Endpoint = "https://awx.example", Token = "static", JobTemplateId = 7 },
            new RecordingSecretStore(),
            handler);

        var result = await client.GetJobStatusAsync("42");

        Assert.Null(result.ElapsedSeconds);
    }

    [Fact]
    public async Task GetJobOutput_returns_the_raw_stdout_text()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal("/api/v2/jobs/42/stdout/", request.RequestUri!.AbsolutePath);
            Assert.Contains("format=txt", request.RequestUri.Query);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("PLAY [deploy] ***\nok: [host]") };
        });
        using var client = new AwxClient(
            new AwxOptions { Endpoint = "https://awx.example", Token = "static", JobTemplateId = 7 },
            new RecordingSecretStore(),
            handler);

        var output = await client.GetJobOutputAsync("42");

        Assert.Equal("PLAY [deploy] ***\nok: [host]", output);
    }

    [Fact]
    public async Task GetJobOutput_returns_null_on_404()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new AwxClient(
            new AwxOptions { Endpoint = "https://awx.example", Token = "static", JobTemplateId = 7 },
            new RecordingSecretStore(),
            handler);

        var output = await client.GetJobOutputAsync("42");

        Assert.Null(output);
    }

    [Fact]
    public async Task GetJobOutput_throws_on_other_error_statuses()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") });
        using var client = new AwxClient(
            new AwxOptions { Endpoint = "https://awx.example", Token = "static", JobTemplateId = 7 },
            new RecordingSecretStore(),
            handler);

        await Assert.ThrowsAsync<ValidationException>(() => client.GetJobOutputAsync("42"));
    }
}
