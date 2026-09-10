using Iris.Infrastructure.Integrations;

namespace Iris.Infrastructure.Tests.Integrations;

public sealed class AnsibleExecutionPackageBuilderTests
{
    [Fact]
    public async Task GetStatusAsync_reports_managed_via_awx_when_no_direct_endpoint_is_set()
    {
        // Iris never runs ansible-playbook itself — AWX drives the one real Ansible on the ops
        // server. The old local `ansible-playbook --version` probe was misleading noise on the
        // Iris.Api host; the normal state is "Managed via AWX", not a separate thing to check.
        var builder = new AnsibleExecutionPackageBuilder(new AnsibleOptions { Endpoint = null });

        var status = await builder.GetStatusAsync(probe: true);

        Assert.Equal("Managed via AWX", status.Status);
        Assert.Null(status.Endpoint);
    }

    [Fact]
    public async Task GetStatusAsync_reports_configured_when_a_direct_endpoint_is_set_but_never_probes_it()
    {
        var builder = new AnsibleExecutionPackageBuilder(new AnsibleOptions { Endpoint = "https://ansible.example.com" });

        var probed = await builder.GetStatusAsync(probe: true);
        var unprobed = await builder.GetStatusAsync(probe: false);

        Assert.Equal("Configured", probed.Status);
        Assert.Equal("Configured", unprobed.Status);
        Assert.Equal("https://ansible.example.com", probed.Endpoint);
    }
}
