using Iris.Infrastructure.Integrations;
using Iris.Infrastructure.Processes;
using Iris.Infrastructure.Tests.Processes;

namespace Iris.Infrastructure.Tests.Integrations;

public sealed class AnsibleExecutionPackageBuilderTests
{
    [Fact]
    public async Task GetStatusAsync_reports_not_configured_when_the_endpoint_is_blank()
    {
        // Regression test for a real bug found via manual testing (2026-09-08): this used to
        // report "Configured" based on Playbook, which always carries a non-blank default
        // ("iris-deploy-application.yml") — so it showed "Configured" on a completely
        // untouched install. Endpoint is the actual signal, same convention as OpenBao/AWX.
        var options = new AnsibleOptions { Endpoint = null };
        var runner = new FakeProcessRunner();
        var builder = new AnsibleExecutionPackageBuilder(options, runner);

        var status = await builder.GetStatusAsync(probe: false);

        Assert.Equal("Not configured", status.Status);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task GetStatusAsync_without_probing_reports_configured_without_touching_the_process_runner()
    {
        var options = new AnsibleOptions { Endpoint = "http://localhost:8043" };
        var runner = new FakeProcessRunner();
        var builder = new AnsibleExecutionPackageBuilder(options, runner);

        var status = await builder.GetStatusAsync(probe: false);

        Assert.Equal("Configured", status.Status);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task GetStatusAsync_probes_by_checking_ansible_playbook_is_runnable()
    {
        // Real, meaningful feedback fixed here (2026-09-08: "cliccando test non succede
        // nulla") — Test now actually runs `ansible-playbook --version` via IProcessRunner
        // rather than returning a static value regardless of what Test does.
        var options = new AnsibleOptions { Endpoint = "http://localhost:8043" };
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(0, "ansible-playbook [core 2.16.3]\n", "") };
        var builder = new AnsibleExecutionPackageBuilder(options, runner);

        var status = await builder.GetStatusAsync(probe: true);

        Assert.Equal("Reachable", status.Status);
        var call = Assert.Single(runner.Calls);
        Assert.Equal("ansible-playbook", call.FileName);
        Assert.Equal(new[] { "--version" }, call.Arguments);
    }

    [Fact]
    public async Task GetStatusAsync_probe_reports_unreachable_when_ansible_playbook_is_not_on_PATH()
    {
        var options = new AnsibleOptions { Endpoint = "http://localhost:8043" };
        var runner = new FakeProcessRunner { DefaultResponse = new ProcessResult(-1, "", "The system cannot find the file specified") };
        var builder = new AnsibleExecutionPackageBuilder(options, runner);

        var status = await builder.GetStatusAsync(probe: true);

        Assert.Equal("Unreachable", status.Status);
    }
}
