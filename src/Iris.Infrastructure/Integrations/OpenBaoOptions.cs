namespace Iris.Infrastructure.Integrations;

internal sealed class OpenBaoOptions
{
    public string? Endpoint { get; set; }

    public string? Token { get; set; }

    public string MountPath { get; set; } = "secret";

    public bool UseKvV2 { get; set; } = true;

    public bool IsSecretStoreConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(Token);
}
