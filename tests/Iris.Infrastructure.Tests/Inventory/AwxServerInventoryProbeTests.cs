using System.Text.Json;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Domain.Infrastructure;
using Iris.Domain.Tenancy;
using Iris.Infrastructure.Integrations;
using Iris.Infrastructure.Inventory;

namespace Iris.Infrastructure.Tests.Inventory;

public sealed class AwxServerInventoryProbeTests
{
    private sealed class FakeAwxClient : IAwxClient
    {
        public Func<AwxJobLaunch, AwxJobLaunchResult> OnLaunch { get; set; } =
            _ => new AwxJobLaunchResult(1, "pending", null, null);

        public Func<string, AwxJobStatusResult> OnStatus { get; set; } =
            _ => new AwxJobStatusResult("successful", true, true, null, null);

        public Func<string, AwxHostFactsResult> OnFacts { get; set; } =
            _ => new AwxHostFactsResult(false, null);

        public int StatusCalls { get; private set; }

        public Task<AwxJobLaunchResult> LaunchAsync(AwxJobLaunch launch, CancellationToken cancellationToken = default) =>
            Task.FromResult(OnLaunch(launch));

        public Task<AwxJobStatusResult> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default)
        {
            StatusCalls++;
            return Task.FromResult(OnStatus(jobId));
        }

        public Task<AwxHostFactsResult> GetHostFactsAsync(string hostname, CancellationToken cancellationToken = default) =>
            Task.FromResult(OnFacts(hostname));
    }

    private static ServerNode NewServer(string? hostname = "web-01.internal") => new(
        Guid.NewGuid(), "web-01", hostname, ServerOs.Linux, ServerHostingType.SelfHosted,
        "1.2.3.4", null, ContextKind.Production);

    private static AwxOptions Configured(int? factsJobTemplateId = 42) => new()
    {
        Endpoint = "https://awx.example",
        Token = "token",
        JobTemplateId = 7,
        FactsJobTemplateId = factsJobTemplateId,
    };

    private static AwxHostFactsResult LinuxFacts() => new(
        true,
        Parse("""
        {
            "ansible_processor_vcpus": 4,
            "ansible_memtotal_mb": 8192,
            "ansible_memfree_mb": 3072,
            "ansible_distribution": "Ubuntu",
            "ansible_distribution_version": "24.04",
            "ansible_mounts": [
                { "device": "/dev/sda1", "mount": "/", "fstype": "ext4", "size_total": 300000000000, "size_available": 120000000000 },
                { "device": "tmpfs", "mount": "/dev/shm", "fstype": "tmpfs", "size_total": 4000000000, "size_available": 4000000000 }
            ]
        }
        """));

    private static IReadOnlyDictionary<string, JsonElement> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.Clone();
        }

        return result;
    }

    [Fact]
    public async Task Discover_requires_the_facts_job_template_to_be_configured()
    {
        var probe = new AwxServerInventoryProbe(new FakeAwxClient(), Configured(factsJobTemplateId: null));

        await Assert.ThrowsAsync<ValidationException>(() => probe.DiscoverAsync(NewServer()));
    }

    [Fact]
    public async Task Discover_reports_unreachable_when_the_server_has_no_hostname()
    {
        var probe = new AwxServerInventoryProbe(new FakeAwxClient(), Configured());

        var snapshot = await probe.DiscoverAsync(NewServer(hostname: null));

        Assert.False(snapshot.IsReachable);
        Assert.Contains("hostname", snapshot.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Discover_reports_unreachable_when_the_job_fails()
    {
        var awx = new FakeAwxClient
        {
            OnStatus = _ => new AwxJobStatusResult("failed", true, false, null, "unreachable host"),
        };
        var probe = new AwxServerInventoryProbe(awx, Configured());

        var snapshot = await probe.DiscoverAsync(NewServer());

        Assert.False(snapshot.IsReachable);
        Assert.Equal("unreachable host", snapshot.Error);
    }

    [Fact]
    public async Task Discover_reports_unreachable_when_no_cached_facts_are_found()
    {
        var probe = new AwxServerInventoryProbe(new FakeAwxClient(), Configured());

        var snapshot = await probe.DiscoverAsync(NewServer());

        Assert.False(snapshot.IsReachable);
        Assert.Contains("No cached facts", snapshot.Error);
    }

    [Fact]
    public async Task Discover_parses_cpu_memory_and_disks_and_excludes_pseudo_filesystems()
    {
        var awx = new FakeAwxClient { OnFacts = _ => LinuxFacts() };
        var probe = new AwxServerInventoryProbe(awx, Configured());
        var server = NewServer();

        var snapshot = await probe.DiscoverAsync(server);

        Assert.True(snapshot.IsReachable);
        Assert.Null(snapshot.Error);
        Assert.Equal(4, snapshot.Resources!.CpuCores);
        Assert.Equal(8192, snapshot.Resources.MemoryMb);
        Assert.Equal(3072, snapshot.Resources.FreeMemoryMb);
        Assert.Equal(300, snapshot.Resources.DiskGb);
        Assert.Equal(120, snapshot.Resources.FreeDiskGb);
        Assert.Equal("Ubuntu 24.04", snapshot.OsVersion);
        Assert.Single(snapshot.Disks); // tmpfs excluded
        Assert.Equal("/dev/sda1", snapshot.Disks[0].DeviceName);
        // No port mapping/guessing happens here — the server's own (empty) value passes through.
        Assert.Equal(server.UsedPorts, snapshot.UsedPorts);
    }

    [Fact]
    public async Task Discover_times_out_without_throwing_when_the_job_never_finishes()
    {
        var awx = new FakeAwxClient
        {
            OnStatus = _ => new AwxJobStatusResult("running", false, false, null, null),
        };
        // Tiny poll interval/timeout — this asserts the timeout *path*, not real-world timing.
        var probe = new AwxServerInventoryProbe(awx, Configured(), TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(20));

        var snapshot = await probe.DiscoverAsync(NewServer());

        Assert.False(snapshot.IsReachable);
        Assert.Contains("Timed out", snapshot.Error);
    }
}
