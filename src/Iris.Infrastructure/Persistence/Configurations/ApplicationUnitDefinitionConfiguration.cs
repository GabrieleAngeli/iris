using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUnitDefinitionConfiguration : IEntityTypeConfiguration<ApplicationUnitDefinition>
{
    public void Configure(EntityTypeBuilder<ApplicationUnitDefinition> builder)
    {
        builder.ToTable("ApplicationUnits");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.ApplicationVersionId).IsRequired();
        builder.Property(u => u.Key).IsRequired().HasMaxLength(200);
        builder.Property(u => u.DisplayName).HasMaxLength(200);
        builder.Property(u => u.Kind).HasMaxLength(100);
        builder.Property(u => u.EntryPoint).HasMaxLength(500);
        builder.Property(u => u.ArtifactPath).HasMaxLength(500);
        builder.Property(u => u.ExecutionTargetsJson);
        builder.Property(u => u.ProfilesJson);

        builder.HasIndex(u => u.ApplicationVersionId);
        builder.HasIndex(u => new { u.ApplicationVersionId, u.Key }).IsUnique();
    }
}
