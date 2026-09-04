using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationVersionConfiguration : IEntityTypeConfiguration<ApplicationVersion>
{
    public void Configure(EntityTypeBuilder<ApplicationVersion> builder)
    {
        builder.ToTable("ApplicationVersions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.ApplicationId).IsRequired();
        builder.Property(v => v.Version).IsRequired().HasMaxLength(100);
        builder.Property(v => v.SourceReference).HasMaxLength(500);

        builder.HasIndex(v => new { v.ApplicationId, v.Version }).IsUnique();

        builder.OwnsOne(v => v.RuntimeMetadata, metadata =>
        {
            metadata.Property(m => m.RuntimeName).IsRequired().HasMaxLength(200).HasColumnName("RuntimeName");
            metadata.Property(m => m.PreferredOs).HasConversion<string>().HasMaxLength(20).HasColumnName("PreferredOs");
            metadata.Property(m => m.RequiredCpuCores).HasColumnName("RequiredCpuCores");
            metadata.Property(m => m.RequiredMemoryMb).HasColumnName("RequiredMemoryMb");

            // Native EF Core primitive collection — mapped as a JSON array column, no separate table.
            metadata.Property(m => m.RequiredPorts).HasColumnName("RequiredPorts");
            metadata.Property(m => m.ExecutionTargetsJson).HasColumnName("ExecutionTargetsJson");
            metadata.Property(m => m.OsSupportJson).HasColumnName("OsSupportJson");
            metadata.Property(m => m.MinimumCpuCores).HasColumnName("MinimumCpuCores");
            metadata.Property(m => m.MinimumMemoryMb).HasColumnName("MinimumMemoryMb");
            metadata.Property(m => m.PortKeysJson).HasColumnName("PortKeysJson");
        });
        builder.Navigation(v => v.RuntimeMetadata).IsRequired();

        // Same treatment: a plain scalar collection, not a navigation to related entities.
        builder.Property(v => v.ImportWarnings);

        builder.Property(v => v.RawImportPackageJson);
        builder.Property(v => v.LastImportSchemaVersion).HasMaxLength(50);
        builder.Property(v => v.LastImportedAtUtc);

        builder.Property(v => v.CreatedAtUtc);
        builder.Property(v => v.UpdatedAtUtc);

        builder.HasMany(v => v.ConfigurationKeys)
            .WithOne()
            .HasForeignKey(k => k.ApplicationVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Dependencies)
            .WithOne()
            .HasForeignKey(d => d.ApplicationVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Placeholders)
            .WithOne()
            .HasForeignKey(p => p.ApplicationVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.ApplicationUnits)
            .WithOne()
            .HasForeignKey(u => u.ApplicationVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.InstallationProfiles)
            .WithOne()
            .HasForeignKey(p => p.ApplicationVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.DependencyConstraints)
            .WithOne()
            .HasForeignKey(c => c.ApplicationVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(ApplicationVersion.ConfigurationKeys))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(ApplicationVersion.Dependencies))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(ApplicationVersion.Placeholders))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(ApplicationVersion.ApplicationUnits))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(ApplicationVersion.InstallationProfiles))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(ApplicationVersion.DependencyConstraints))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
