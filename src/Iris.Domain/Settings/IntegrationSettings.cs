using Iris.Domain.Common;

namespace Iris.Domain.Settings;

/// <summary>
/// Where the OpenBao/AWX/Ansible integration endpoints are configured — persisted so they
/// can be set from the UI instead of <c>appsettings.json</c>/environment variables only.
/// Single-row, like <see cref="MailProviderSettings"/>, but with three independent
/// mutators (one per integration group) rather than one whole-row <c>Configure</c> factory:
/// OpenBao/AWX/Ansible are saved from three separate requests and none should wipe the
/// other two's fields.
///
/// <see cref="OpenBaoTokenSecretReference"/>/<see cref="AwxTokenSecretReference"/> are
/// opaque references — the real token value lives in <c>ISecretStore</c>, never here.
/// </summary>
public sealed class IntegrationSettings : Entity<Guid>, IAggregateRoot, IAuditableEntity
{
    /// <summary>The one and only row this table ever holds.</summary>
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    // For the persistence layer.
    private IntegrationSettings()
        : base(Guid.Empty)
    {
        OpenBaoMountPath = "secret";
        AnsiblePlaybook = "iris-deploy-application.yml";
    }

    private IntegrationSettings(Guid id)
        : base(id)
    {
        OpenBaoMountPath = "secret";
        AnsiblePlaybook = "iris-deploy-application.yml";
    }

    public string? OpenBaoEndpoint { get; private set; }

    /// <summary>Opaque reference into <c>ISecretStore</c> — never the token itself.</summary>
    public string? OpenBaoTokenSecretReference { get; private set; }

    public string OpenBaoMountPath { get; private set; }

    public bool OpenBaoUseKvV2 { get; private set; } = true;

    public string? AwxEndpoint { get; private set; }

    /// <summary>Opaque reference into <c>ISecretStore</c> — never the token itself.</summary>
    public string? AwxTokenSecretReference { get; private set; }

    public int? AwxJobTemplateId { get; private set; }

    public string? AnsibleEndpoint { get; private set; }

    public string AnsiblePlaybook { get; private set; }

    public string? AnsibleInventory { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Creates the (only ever one) row, with every group unconfigured. Callers
    /// then call one or more of the <c>ConfigureXxx</c> methods before saving.</summary>
    public static IntegrationSettings CreateEmpty() => new(SingletonId);

    public void ConfigureOpenBao(string endpoint, string? tokenSecretReference, string mountPath, bool useKvV2)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(mountPath);

        OpenBaoEndpoint = endpoint.Trim();
        OpenBaoTokenSecretReference = tokenSecretReference;
        OpenBaoMountPath = mountPath.Trim();
        OpenBaoUseKvV2 = useKvV2;
    }

    public void ConfigureAwx(string endpoint, string? tokenSecretReference, int? jobTemplateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        AwxEndpoint = endpoint.Trim();
        AwxTokenSecretReference = tokenSecretReference;
        AwxJobTemplateId = jobTemplateId;
    }

    public void ConfigureAnsible(string endpoint, string playbook, string? inventory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(playbook);

        AnsibleEndpoint = endpoint.Trim();
        AnsiblePlaybook = playbook.Trim();
        AnsibleInventory = string.IsNullOrWhiteSpace(inventory) ? null : inventory.Trim();
    }
}
