namespace Iris.Infrastructure.Integrations;

internal sealed class NexusOptions
{
    public string? Endpoint { get; set; }

    /// <summary>Stored for future real artifact operations — not required for the reachability
    /// check itself, since Nexus's own status endpoint (<c>/service/rest/v1/status</c>) is
    /// anonymous.</summary>
    public string? Token { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);
}
