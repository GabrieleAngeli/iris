namespace Iris.Infrastructure.Integrations;

internal sealed class AnsibleOptions
{
    public string? Endpoint { get; init; }

    public string Playbook { get; init; } = "iris-deploy-application.yml";

    public string? Inventory { get; init; }
}
