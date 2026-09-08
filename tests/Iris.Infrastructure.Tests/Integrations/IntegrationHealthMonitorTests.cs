using Iris.Infrastructure.Integrations;

namespace Iris.Infrastructure.Tests.Integrations;

public sealed class IntegrationHealthMonitorTests
{
    [Fact]
    public void GetSnapshot_returns_null_before_anything_is_recorded()
    {
        var monitor = new IntegrationHealthMonitor();

        Assert.Null(monitor.GetSnapshot("openbao"));
    }

    [Fact]
    public void Record_then_GetSnapshot_roundtrips()
    {
        var monitor = new IntegrationHealthMonitor();
        var checkedAt = DateTimeOffset.UtcNow;

        monitor.Record("openbao", "Reachable", "200 OK", checkedAt);
        var snapshot = monitor.GetSnapshot("openbao");

        Assert.NotNull(snapshot);
        Assert.Equal("Reachable", snapshot!.Status);
        Assert.Equal("200 OK", snapshot.Message);
        Assert.Equal(checkedAt, snapshot.CheckedAtUtc);
    }

    [Fact]
    public void Record_overwrites_the_previous_snapshot_for_the_same_key()
    {
        var monitor = new IntegrationHealthMonitor();

        monitor.Record("openbao", "Reachable", null, DateTimeOffset.UtcNow.AddMinutes(-5));
        monitor.Record("openbao", "Unreachable", "timed out", DateTimeOffset.UtcNow);

        Assert.Equal("Unreachable", monitor.GetSnapshot("openbao")!.Status);
    }

    [Fact]
    public void GetSnapshot_is_case_insensitive_on_key()
    {
        var monitor = new IntegrationHealthMonitor();
        monitor.Record("OpenBao", "Reachable", null, DateTimeOffset.UtcNow);

        Assert.NotNull(monitor.GetSnapshot("openbao"));
    }
}
