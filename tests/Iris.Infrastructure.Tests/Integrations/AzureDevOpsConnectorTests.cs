using System.Net;
using Iris.Infrastructure.Integrations;

namespace Iris.Infrastructure.Tests.Integrations;

public sealed class AzureDevOpsConnectorTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Method, string Path, string Query, string? AuthScheme)> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request.Method.Method, request.RequestUri!.AbsolutePath, request.RequestUri.Query, request.Headers.Authorization?.Scheme));
            return Task.FromResult(respond(request));
        }
    }

    private static AzureDevOpsOptions Options() => new()
    {
        Endpoint = "https://dev.azure.com/algorab-devops",
        Token = "pat-value",
    };

    [Fact]
    public async Task GetFileContent_requests_the_expected_url_with_basic_auth_and_returns_raw_text()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("awx_context_blueprints: []") });
        using var connector = new AzureDevOpsConnector(Options(), handler);

        var content = await connector.GetFileContentAsync(
            "Refactoring_ops_flow", "awx", "master", "automation/manifests/awx_context_blueprints.yml");

        Assert.Equal("awx_context_blueprints: []", content);
        var call = Assert.Single(handler.Calls);
        Assert.Equal("GET", call.Method);
        Assert.Equal("/algorab-devops/Refactoring_ops_flow/_apis/git/repositories/awx/items", call.Path);
        Assert.Contains("versionDescriptor.version=master", call.Query);
        Assert.Equal("Basic", call.AuthScheme);
    }

    [Fact]
    public async Task GetFileContent_returns_null_on_404()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var connector = new AzureDevOpsConnector(Options(), handler);

        var content = await connector.GetFileContentAsync("Refactoring_ops_flow", "awx", "master", "missing.yml");

        Assert.Null(content);
    }

    [Fact]
    public async Task GetFileContent_throws_on_other_error_statuses()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("no access") });
        using var connector = new AzureDevOpsConnector(Options(), handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            connector.GetFileContentAsync("Refactoring_ops_flow", "awx", "master", "secret.yml"));
    }
}
