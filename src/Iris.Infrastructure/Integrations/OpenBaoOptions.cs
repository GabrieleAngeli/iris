namespace Iris.Infrastructure.Integrations;

internal sealed class OpenBaoOptions
{
    public string? Endpoint { get; init; }

    public string? Token { get; init; }

    public string MountPath { get; init; } = "secret";

    public bool UseKvV2 { get; init; } = true;

    public bool IsSecretStoreConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(Token);
}
