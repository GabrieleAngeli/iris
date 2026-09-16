namespace Iris.Infrastructure.Integrations;

internal sealed class AzureDevOpsOptions
{
    /// <summary>Organization URL, e.g. <c>https://dev.azure.com/your-org</c>.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Personal Access Token — sent as the password half of HTTP Basic auth (empty
    /// username), Azure DevOps's own convention for PAT authentication.</summary>
    public string? Token { get; set; }

    /// <summary>A PAT is required for any real API call — unlike Nexus's anonymous status
    /// endpoint, Azure DevOps has no meaningful unauthenticated check.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Token);

    /// <summary>Project holding the AWX automation repository.</summary>
    public string? Project { get; set; }

    /// <summary>Repository name inside <see cref="Project"/> (e.g. <c>awx</c>).</summary>
    public string? Repository { get; set; }

    public string Branch { get; set; } = "master";

    /// <summary>Path, inside the repository, to the AWX context blueprint manifest — the
    /// declared-state side of <c>AwxBlueprintDriftConnector</c>'s comparison.</summary>
    public string ManifestPath { get; set; } = "automation/manifests/awx_context_blueprints.yml";

    public bool CanReadManifest =>
        IsConfigured && !string.IsNullOrWhiteSpace(Project) && !string.IsNullOrWhiteSpace(Repository);
}
