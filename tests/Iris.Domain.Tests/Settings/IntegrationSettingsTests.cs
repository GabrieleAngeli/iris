using Iris.Domain.Settings;

namespace Iris.Domain.Tests.Settings;

public sealed class IntegrationSettingsTests
{
    [Fact]
    public void CreateEmpty_has_the_singleton_id_and_default_fields()
    {
        var settings = IntegrationSettings.CreateEmpty();

        Assert.Equal(IntegrationSettings.SingletonId, settings.Id);
        Assert.Null(settings.OpenBaoEndpoint);
        Assert.Null(settings.AwxEndpoint);
        Assert.Null(settings.AnsibleEndpoint);
        Assert.Equal("secret", settings.OpenBaoMountPath);
        Assert.True(settings.OpenBaoUseKvV2);
        Assert.Equal("iris-deploy-application.yml", settings.AnsiblePlaybook);
    }

    [Fact]
    public void ConfigureOpenBao_sets_fields_and_trims_input()
    {
        var settings = IntegrationSettings.CreateEmpty();

        settings.ConfigureOpenBao(" https://openbao.example.com ", "openbao://secret/token", " secret ", useKvV2: false);

        Assert.Equal("https://openbao.example.com", settings.OpenBaoEndpoint);
        Assert.Equal("openbao://secret/token", settings.OpenBaoTokenSecretReference);
        Assert.Equal("secret", settings.OpenBaoMountPath);
        Assert.False(settings.OpenBaoUseKvV2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConfigureOpenBao_rejects_a_blank_endpoint(string? endpoint)
    {
        var settings = IntegrationSettings.CreateEmpty();

        // ThrowIfNullOrWhiteSpace throws ArgumentNullException specifically for null and plain
        // ArgumentException for empty/whitespace — ThrowsAny accepts either, since both are the
        // "blank endpoint was rejected" outcome this test cares about.
        Assert.ThrowsAny<ArgumentException>(() => settings.ConfigureOpenBao(endpoint!, null, "secret", true));
    }

    [Fact]
    public void ConfigureAwx_sets_fields()
    {
        var settings = IntegrationSettings.CreateEmpty();

        settings.ConfigureAwx("https://awx.example.com", "openbao://secret/awx-token", 42);

        Assert.Equal("https://awx.example.com", settings.AwxEndpoint);
        Assert.Equal("openbao://secret/awx-token", settings.AwxTokenSecretReference);
        Assert.Equal(42, settings.AwxJobTemplateId);
    }

    [Fact]
    public void ConfigureAwx_stores_the_optional_oauth_refresh_fields()
    {
        var settings = IntegrationSettings.CreateEmpty();

        settings.ConfigureAwx(
            "https://awx.example.com", "ref://awx/token", 42,
            oAuthClientId: "  client-abc  ",
            oAuthClientSecretReference: "ref://awx/oauth-client-secret",
            refreshTokenSecretReference: "ref://awx/refresh-token");

        Assert.Equal("client-abc", settings.AwxOAuthClientId);
        Assert.Equal("ref://awx/oauth-client-secret", settings.AwxOAuthClientSecretReference);
        Assert.Equal("ref://awx/refresh-token", settings.AwxRefreshTokenSecretReference);
    }

    [Fact]
    public void ConfigureAwx_rejects_a_blank_endpoint()
    {
        var settings = IntegrationSettings.CreateEmpty();

        Assert.Throws<ArgumentException>(() => settings.ConfigureAwx("", null, null));
    }

    [Fact]
    public void ConfigureAnsible_sets_fields_and_normalizes_blank_inventory_to_null()
    {
        var settings = IntegrationSettings.CreateEmpty();

        settings.ConfigureAnsible("https://ansible.example.com", "deploy.yml", "   ");

        Assert.Equal("https://ansible.example.com", settings.AnsibleEndpoint);
        Assert.Equal("deploy.yml", settings.AnsiblePlaybook);
        Assert.Null(settings.AnsibleInventory);
    }

    [Fact]
    public void ConfigureAnsible_rejects_a_blank_endpoint_or_playbook()
    {
        var settings = IntegrationSettings.CreateEmpty();

        Assert.Throws<ArgumentException>(() => settings.ConfigureAnsible("", "deploy.yml", null));
        Assert.Throws<ArgumentException>(() => settings.ConfigureAnsible("https://ansible.example.com", "", null));
    }

    [Fact]
    public void ConfigureOpenBao_does_not_disturb_Awx_or_Ansible_fields()
    {
        var settings = IntegrationSettings.CreateEmpty();
        settings.ConfigureAwx("https://awx.example.com", "ref", 1);
        settings.ConfigureAnsible("https://ansible.example.com", "deploy.yml", "hosts.ini");

        settings.ConfigureOpenBao("https://openbao.example.com", "ref2", "secret", true);

        Assert.Equal("https://awx.example.com", settings.AwxEndpoint);
        Assert.Equal("https://ansible.example.com", settings.AnsibleEndpoint);
    }

    [Fact]
    public void ConfigureAzureDevOps_sets_fields_and_trims_input()
    {
        var settings = IntegrationSettings.CreateEmpty();

        settings.ConfigureAzureDevOps(" https://dev.azure.com/contoso ", "openbao://secret/ado-token");

        Assert.Equal("https://dev.azure.com/contoso", settings.AzureDevOpsEndpoint);
        Assert.Equal("openbao://secret/ado-token", settings.AzureDevOpsTokenSecretReference);
    }

    [Fact]
    public void ConfigureAzureDevOps_rejects_a_blank_endpoint()
    {
        var settings = IntegrationSettings.CreateEmpty();

        Assert.ThrowsAny<ArgumentException>(() => settings.ConfigureAzureDevOps("", null));
    }

    [Fact]
    public void ConfigureNexus_sets_fields_and_trims_input()
    {
        var settings = IntegrationSettings.CreateEmpty();

        settings.ConfigureNexus(" https://nexus.example.com ", "openbao://secret/nexus-token");

        Assert.Equal("https://nexus.example.com", settings.NexusEndpoint);
        Assert.Equal("openbao://secret/nexus-token", settings.NexusTokenSecretReference);
    }

    [Fact]
    public void ConfigureNexus_rejects_a_blank_endpoint()
    {
        var settings = IntegrationSettings.CreateEmpty();

        Assert.ThrowsAny<ArgumentException>(() => settings.ConfigureNexus("", null));
    }
}
