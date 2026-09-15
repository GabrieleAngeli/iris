namespace Iris.Application.Abstractions;

/// <summary>Reads a single file's raw content out of an Azure DevOps git repository — used to
/// fetch the AWX automation repo's blueprint manifest for drift detection
/// (<c>AwxBlueprintDriftConnector</c>). Iris never writes to this repository, only reads it.</summary>
public interface IAzureDevOpsRepositoryReader
{
    /// <summary>Returns the file's raw text content, or null if it doesn't exist at that path/branch.</summary>
    Task<string?> GetFileContentAsync(
        string project,
        string repository,
        string branch,
        string path,
        CancellationToken cancellationToken = default);
}
