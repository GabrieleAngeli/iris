using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationInstallationConfiguration : IEntityTypeConfiguration<ApplicationInstallation>
{
    public void Configure(EntityTypeBuilder<ApplicationInstallation> builder)
    {
        builder.ToTable("ApplicationInstallations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Name).IsRequired().HasMaxLength(200);
        builder.Property(i => i.ApplicationId).IsRequired();
        builder.Property(i => i.ApplicationVersionId).IsRequired();
        builder.Property(i => i.ApplicationUnitKey).HasMaxLength(200);
        builder.Property(i => i.InstallationProfileKey).HasMaxLength(200);
        builder.Property(i => i.ServerNodeId).IsRequired();
        builder.Property(i => i.Environment).HasConversion<string>().HasMaxLength(40);
        builder.Property(i => i.Notes).HasMaxLength(1000);
        builder.Property(i => i.IsActive);
        builder.Property(i => i.CreatedAtUtc);
        builder.Property(i => i.UpdatedAtUtc);

        builder.HasIndex(i => i.ApplicationId);
        builder.HasIndex(i => i.ApplicationVersionId);
        builder.HasIndex(i => i.ServerNodeId);

        builder.HasMany(i => i.Bindings)
            .WithOne()
            .HasForeignKey(b => b.ApplicationInstallationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(ApplicationInstallation.Bindings))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
