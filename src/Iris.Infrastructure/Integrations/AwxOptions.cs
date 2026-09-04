namespace Iris.Infrastructure.Integrations;

internal sealed class AwxOptions
{
    public string? Endpoint { get; init; }

    public string? Token { get; init; }

    public int? JobTemplateId { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(Token) &&
        JobTemplateId is > 0;
}
