namespace Iris.Domain.Infrastructure;

/// <summary>
/// Resource hints for a <see cref="ServerNode"/> — what capacity it has, as far as the
/// operator knows it (not a fact the platform measures). Owned by its <see cref="ServerNode"/>:
/// no identity of its own, replaced wholesale on update. All fields are optional: an operator
/// may know CPU/RAM but not disk, or nothing at all yet. Disk can be described both as
/// total capacity and as the operator-reserved slices for application data and backups.
/// </summary>
public sealed class ResourceProfile
{
    // For the persistence layer.
    private ResourceProfile()
    {
    }

    public ResourceProfile(
        int? cpuCores,
        int? memoryMb,
        int? diskGb,
        int? applicationDiskGb = null,
        int? backupDiskGb = null)
    {
        CpuCores = cpuCores;
        MemoryMb = memoryMb;
        DiskGb = diskGb;
        ApplicationDiskGb = applicationDiskGb;
        BackupDiskGb = backupDiskGb;
    }

    public int? CpuCores { get; private set; }

    public int? MemoryMb { get; private set; }

    public int? DiskGb { get; private set; }

    public int? ApplicationDiskGb { get; private set; }

    public int? BackupDiskGb { get; private set; }
}
