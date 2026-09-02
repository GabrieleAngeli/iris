namespace Iris.Domain.Infrastructure;

/// <summary>
/// Resource hints for a <see cref="ServerNode"/> — what capacity it has, as far as the
/// operator knows it (not a fact the platform measures). Owned by its <see cref="ServerNode"/>:
/// no identity of its own, replaced wholesale on update. All fields are optional: an operator
/// may know CPU/RAM but not disk, or nothing at all yet.
/// </summary>
public sealed class ResourceProfile
{
    // For the persistence layer.
    private ResourceProfile()
    {
    }

    public ResourceProfile(int? cpuCores, int? memoryMb, int? diskGb)
    {
        CpuCores = cpuCores;
        MemoryMb = memoryMb;
        DiskGb = diskGb;
    }

    public int? CpuCores { get; private set; }

    public int? MemoryMb { get; private set; }

    public int? DiskGb { get; private set; }
}
