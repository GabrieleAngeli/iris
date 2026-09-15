using System.Text.Json;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Domain.Infrastructure;
using Iris.Infrastructure.Integrations;

namespace Iris.Infrastructure.Inventory;

/// <summary>
/// Real server inventory discovery: launches AWX's dedicated facts job template (a Job Template
/// configured with <c>use_fact_cache=True</c>) restricted to one host, polls it to completion,
/// then reads the host's cached <c>ansible_facts</c> back via <see cref="IAwxClient.GetHostFactsAsync"/>.
/// Deliberately never touches <see cref="ServerNode.UsedPorts"/> — no port is ever mapped or
/// guessed here; it is passed straight through unchanged.
/// </summary>
internal sealed class AwxServerInventoryProbe(
    IAwxClient awx,
    AwxOptions options,
    TimeSpan? pollInterval = null,
    TimeSpan? pollTimeout = null) : IServerInventoryProbe
{
    // Kept comfortably under the MAUI client's default HttpClient timeout (100s, unset in
    // MauiProgram.cs) so a slow-but-succeeding job reports back as "timed out" from here
    // (a clear, persisted ServerNode.LastDiscoveryError) rather than the client-side request
    // aborting first with no server-side record of what happened. Overridable (test seam) so
    // a "never finishes" test doesn't have to wait out the real timeout.
    private readonly TimeSpan _pollInterval = pollInterval ?? TimeSpan.FromSeconds(3);
    private readonly TimeSpan _pollTimeout = pollTimeout ?? TimeSpan.FromSeconds(80);

    /// <summary>Ansible mount filesystem types that aren't real, space-bearing disks.</summary>
    private static readonly HashSet<string> PseudoFileSystems = new(StringComparer.OrdinalIgnoreCase)
    {
        "tmpfs", "devtmpfs", "overlay", "squashfs", "proc", "sysfs", "cgroup", "cgroup2", "devfs", "autofs",
    };

    public async Task<ServerInventorySnapshot> DiscoverAsync(ServerNode server, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (options.FactsJobTemplateId is not > 0)
        {
            throw new ValidationException("Configure the AWX facts job template first.");
        }

        if (string.IsNullOrWhiteSpace(server.Hostname))
        {
            return ServerInventorySnapshot.Unreachable(
                "The server has no hostname set — AWX can't match it to an inventory host.");
        }

        AwxJobLaunchResult launch;
        try
        {
            launch = await awx.LaunchAsync(
                new AwxJobLaunch(options.FactsJobTemplateId, BuildPackage(server)), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ValidationException ex)
        {
            return ServerInventorySnapshot.Unreachable($"Could not launch the AWX facts job: {ex.Message}");
        }

        var jobId = launch.JobId.ToString();
        var status = await PollUntilFinishedAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (status is null)
        {
            return ServerInventorySnapshot.Unreachable($"Timed out waiting for the AWX facts job (job {jobId}) to finish.");
        }

        if (!status.Succeeded)
        {
            return ServerInventorySnapshot.Unreachable(
                status.Message ?? $"AWX facts job {jobId} did not succeed (status: {status.Status}).");
        }

        AwxHostFactsResult factsResult;
        try
        {
            factsResult = await awx.GetHostFactsAsync(options.FactsJobTemplateId!.Value, server.Hostname, cancellationToken).ConfigureAwait(false);
        }
        catch (ValidationException ex)
        {
            return ServerInventorySnapshot.Unreachable($"Could not retrieve AWX facts: {ex.Message}");
        }

        if (!factsResult.HostFound || factsResult.Facts is not { Count: > 0 } facts)
        {
            return ServerInventorySnapshot.Unreachable(
                $"No cached facts found for host '{server.Hostname}' in AWX — check it exists in the facts " +
                "job template's inventory and that the template has 'use_fact_cache' enabled.");
        }

        return BuildSnapshot(server, facts);
    }

    private static AnsibleExecutionPackage BuildPackage(ServerNode server) => new(
        Playbook: "gather_facts",
        Inventory: null,
        Limit: server.Hostname,
        CheckMode: false,
        ExtraVars: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["iris_server_id"] = server.Id.ToString(),
            ["iris_server_name"] = server.Hostname,
        });

    /// <summary>Polls until the job finishes or <see cref="PollTimeout"/> elapses. Returns
    /// <c>null</c> on timeout.</summary>
    private async Task<AwxJobStatusResult?> PollUntilFinishedAsync(string jobId, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + _pollTimeout;
        while (true)
        {
            var status = await awx.GetJobStatusAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (status.Finished)
            {
                return status;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return null;
            }

            await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static ServerInventorySnapshot BuildSnapshot(ServerNode server, IReadOnlyDictionary<string, JsonElement> facts)
    {
        var cpuCores = ReadInt(facts, "ansible_processor_vcpus")
            ?? ReadInt(facts, "ansible_processor_cores")
            ?? ReadInt(facts, "ansible_processor_count");
        var memTotalMb = ReadInt(facts, "ansible_memtotal_mb");
        var memFreeMb = ReadInt(facts, "ansible_memfree_mb");

        var disks = ReadDisks(facts);
        var diskGb = disks.Count > 0 ? disks.Sum(d => d.TotalGb) : (int?)null;
        var freeDiskGb = disks.Count > 0 ? disks.Sum(d => d.FreeGb) : (int?)null;

        var osVersion = ReadOsVersion(facts);
        var machineSize = cpuCores is { } cores && memTotalMb is { } mem
            ? $"{cores} vCPU / {mem / 1024.0:0.#} GB RAM"
            : server.MachineSize;

        var resources = new ResourceProfile(
            cpuCores,
            memTotalMb,
            diskGb,
            server.Resources?.ApplicationDiskGb,
            server.Resources?.BackupDiskGb,
            memFreeMb,
            freeDiskGb);

        return new ServerInventorySnapshot(
            IsReachable: true,
            Error: null,
            Os: server.Os,
            OsVersion: osVersion,
            MachineSize: machineSize,
            Capabilities: server.Capabilities,
            Resources: resources,
            UsedPorts: server.UsedPorts,
            Disks: disks);
    }

    private static List<ServerDiskInput> ReadDisks(IReadOnlyDictionary<string, JsonElement> facts)
    {
        var disks = new List<ServerDiskInput>();
        if (!facts.TryGetValue("ansible_mounts", out var mounts) || mounts.ValueKind != JsonValueKind.Array)
        {
            return disks;
        }

        foreach (var mount in mounts.EnumerateArray())
        {
            var fileSystem = mount.TryGetProperty("fstype", out var fsProperty) ? fsProperty.GetString() : null;
            if (fileSystem is not null && PseudoFileSystems.Contains(fileSystem))
            {
                continue;
            }

            var device = mount.TryGetProperty("device", out var deviceProperty) ? deviceProperty.GetString() : null;
            if (string.IsNullOrWhiteSpace(device))
            {
                continue;
            }

            var mountPoint = mount.TryGetProperty("mount", out var mountProperty) ? mountProperty.GetString() : null;
            var totalBytes = mount.TryGetProperty("size_total", out var totalProperty) && totalProperty.TryGetInt64(out var total)
                ? total
                : 0;
            var freeBytes = mount.TryGetProperty("size_available", out var freeProperty) && freeProperty.TryGetInt64(out var free)
                ? free
                : 0;

            disks.Add(new ServerDiskInput(device, mountPoint, fileSystem, BytesToGb(totalBytes), BytesToGb(freeBytes)));
        }

        return disks;
    }

    private static string? ReadOsVersion(IReadOnlyDictionary<string, JsonElement> facts)
    {
        var distribution = ReadString(facts, "ansible_distribution");
        var version = ReadString(facts, "ansible_distribution_version");
        if (!string.IsNullOrWhiteSpace(distribution))
        {
            return string.IsNullOrWhiteSpace(version) ? distribution : $"{distribution} {version}";
        }

        return ReadString(facts, "ansible_os_name");
    }

    private static string? ReadString(IReadOnlyDictionary<string, JsonElement> facts, string key) =>
        facts.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? ReadInt(IReadOnlyDictionary<string, JsonElement> facts, string key)
    {
        if (!facts.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var parsedNumber) => parsedNumber,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsedString) => parsedString,
            _ => null,
        };
    }

    private static int BytesToGb(long bytes) => (int)Math.Round(bytes / 1_000_000_000.0, MidpointRounding.AwayFromZero);
}
