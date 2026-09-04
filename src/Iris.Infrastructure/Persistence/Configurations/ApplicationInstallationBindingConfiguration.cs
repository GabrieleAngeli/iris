using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationInstallationBindingConfiguration : IEntityTypeConfiguration<ApplicationInstallationBinding>
{
    public void Configure(EntityTypeBuilder<ApplicationInstallationBinding> builder)
    {
        builder.ToTable("ApplicationInstallationBindings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.ApplicationInstallationId).IsRequired();
        builder.Property(b => b.PlaceholderKey).IsRequired().HasMaxLength(300);
        builder.Property(b => b.TargetKind).IsRequired().HasMaxLength(80);
        builder.Property(b => b.TargetId);
        builder.Property(b => b.TargetSlug).HasMaxLength(200);
        builder.Property(b => b.ValuePreview).HasMaxLength(1000);
        builder.Property(b => b.Notes).HasMaxLength(1000);

        builder.HasIndex(b => b.ApplicationInstallationId);
    }
}
