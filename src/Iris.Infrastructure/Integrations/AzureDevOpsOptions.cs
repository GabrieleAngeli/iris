namespace Iris.Infrastructure.Integrations;

internal sealed class AzureDevOpsOptions
{
    /// <summary>Organization URL, e.g. <c>https://dev.azure.com/your-org</c>.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Personal Access Token — sent as the password half of HTTP Basic auth (empty
    /// username), Azure DevOps's own convention for PAT authentication.</summary>
    public string? Token { get; init; }

    /// <summary>A PAT is required for any real API call — unlike Nexus's anonymous status
    /// endpoint, Azure DevOps has no meaningful unauthenticated check.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Token);
}
