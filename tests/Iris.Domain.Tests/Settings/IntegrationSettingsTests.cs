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
}
