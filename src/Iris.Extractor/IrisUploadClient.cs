using System.Net.Http.Headers;
using System.Net.Http.Json;
using Iris.Contracts.Applications;

namespace Iris.Extractor;

/// <summary>Thin wrapper around <c>POST /applications/{applicationId}/versions/{versionId}/import</c> —
/// the optional last step so a pipeline doesn't need a separate <c>curl</c> stage.</summary>
internal sealed class IrisUploadClient(string baseUrl)
{
    private static readonly HttpClient Http = new();

    public async Task<bool> UploadAsync(
        string applicationId,
        string versionId,
        string token,
        ImportConfigurationPackageRequest package,
        CancellationToken cancellationToken)
    {
        var url = $"{baseUrl.TrimEnd('/')}/applications/{applicationId}/versions/{versionId}/import";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(package, options: PackageJsonOptions.Instance),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Imported configuration package into {url}.");
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Console.Error.WriteLine($"Import failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");
        return false;
    }
}
