using Iris.Domain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class ServerNodeConfiguration : IEntityTypeConfiguration<ServerNode>
{
    public void Configure(EntityTypeBuilder<ServerNode> builder)
    {
        builder.ToTable("Servers");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Hostname).HasMaxLength(260);
        builder.Property(s => s.OsVersion).HasMaxLength(120);
        builder.Property(s => s.MachineSize).HasMaxLength(120);

        builder.Property(s => s.Os).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.HostingType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Environment).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(s => s.PublicIpAddress).HasMaxLength(45);
        builder.Property(s => s.PrivateIpAddress).HasMaxLength(45);
        builder.Property(s => s.IsActive);

        // Plain scalar collections, not navigations — EF Core maps them as native primitive
        // collections, same treatment as ApplicationVersion.RequiredPorts/ImportWarnings.
        builder.PrimitiveCollection(s => s.Capabilities).ElementType(e => e.HasConversion<string>());
        builder.Property(s => s.UsedPorts);

        builder.OwnsOne(s => s.Resources, resources =>
        {
            resources.Property(r => r.CpuCores).HasColumnName("ResourceCpuCores");
            resources.Property(r => r.MemoryMb).HasColumnName("ResourceMemoryMb");
            resources.Property(r => r.DiskGb).HasColumnName("ResourceDiskGb");
            resources.Property(r => r.ApplicationDiskGb).HasColumnName("ResourceApplicationDiskGb");
            resources.Property(r => r.BackupDiskGb).HasColumnName("ResourceBackupDiskGb");
            resources.Property(r => r.FreeMemoryMb).HasColumnName("ResourceFreeMemoryMb");
            resources.Property(r => r.FreeDiskGb).HasColumnName("ResourceFreeDiskGb");
        });
        builder.Navigation(s => s.Resources).IsRequired(false);

        builder.Property(s => s.IsReachable);
        builder.Property(s => s.LastDiscoveredAtUtc);
        builder.Property(s => s.LastDiscoveryError).HasMaxLength(2000);

        builder.Property(s => s.CreatedAtUtc);
        builder.Property(s => s.UpdatedAtUtc);

        builder.HasMany(s => s.Credentials)
            .WithOne()
            .HasForeignKey(c => c.ServerNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(ServerNode.Credentials))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(s => s.Disks)
            .WithOne()
            .HasForeignKey(d => d.ServerNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(ServerNode.Disks))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ServerDiskConfiguration : IEntityTypeConfiguration<ServerDisk>
{
    public void Configure(EntityTypeBuilder<ServerDisk> builder)
    {
        builder.ToTable("ServerDisks");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.DeviceName).IsRequired().HasMaxLength(260);
        builder.Property(d => d.MountPoint).HasMaxLength(260);
        builder.Property(d => d.FileSystem).HasMaxLength(60);
        builder.Property(d => d.TotalGb);
        builder.Property(d => d.FreeGb);
    }
}
