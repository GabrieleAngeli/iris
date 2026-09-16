namespace Iris.Infrastructure.Integrations;

internal sealed class AnsibleOptions
{
    public string? Endpoint { get; set; }

    public string Playbook { get; set; } = "iris-deploy-application.yml";

    public string? Inventory { get; set; }
}
