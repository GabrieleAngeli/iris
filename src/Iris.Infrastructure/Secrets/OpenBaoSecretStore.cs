using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Iris.Application.Abstractions;
using Iris.Infrastructure.Integrations;

namespace Iris.Infrastructure.Secrets;

internal sealed class OpenBaoSecretStore(OpenBaoOptions options) : ISecretStore, IDisposable
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public async Task<string> StoreAsync(
        string logicalPath,
        string secretValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretValue);
        EnsureConfigured();

        using var request = new HttpRequestMessage(HttpMethod.Post, DataUri(logicalPath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        request.Content = JsonContent.Create(options.UseKvV2
            ? new { data = new Dictionary<string, string> { ["value"] = secretValue } }
            : new Dictionary<string, string> { ["value"] = secretValue });

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return $"openbao://{options.MountPath.Trim('/')}/{logicalPath.Trim('/')}";
    }

    public async Task<string?> RetrieveAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (!TryParseReference(reference, out var logicalPath))
        {
            return null;
        }

        EnsureConfigured();

        using var request = new HttpRequestMessage(HttpMethod.Get, DataUri(logicalPath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = json.RootElement;
        var data = options.UseKvV2 && root.TryGetProperty("data", out var wrapper) && wrapper.TryGetProperty("data", out var nested)
            ? nested
            : root.TryGetProperty("data", out var direct) ? direct : root;
        return data.TryGetProperty("value", out var value) ? value.GetString() : null;
    }

    public async Task DeleteAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (!TryParseReference(reference, out var logicalPath))
        {
            return;
        }

        EnsureConfigured();
        using var request = new HttpRequestMessage(HttpMethod.Delete, DataUri(logicalPath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private Uri DataUri(string logicalPath)
    {
        var mount = options.MountPath.Trim('/');
        var path = logicalPath.Trim('/');
        var segment = options.UseKvV2 ? $"{mount}/data/{path}" : $"{mount}/{path}";
        return new Uri(new Uri(options.Endpoint!), $"/v1/{segment}");
    }

    private void EnsureConfigured()
    {
        if (!options.IsSecretStoreConfigured)
        {
            throw new InvalidOperationException("OpenBao secret store is not configured.");
        }
    }

    private static bool TryParseReference(string reference, out string logicalPath)
    {
        logicalPath = string.Empty;
        if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "openbao", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        logicalPath = uri.AbsolutePath.Trim('/');
        return logicalPath.Length > 0;
    }

    public void Dispose() => _http.Dispose();
}
