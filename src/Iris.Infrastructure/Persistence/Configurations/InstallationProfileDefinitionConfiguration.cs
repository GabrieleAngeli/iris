using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class InstallationProfileDefinitionConfiguration : IEntityTypeConfiguration<InstallationProfileDefinition>
{
    public void Configure(EntityTypeBuilder<InstallationProfileDefinition> builder)
    {
        builder.ToTable("ApplicationInstallationProfiles");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.ApplicationVersionId).IsRequired();
        builder.Property(p => p.Key).IsRequired().HasMaxLength(200);
        builder.Property(p => p.DisplayName).HasMaxLength(200);
        builder.Property(p => p.Required);
        builder.Property(p => p.Multiple);
        builder.Property(p => p.ConfigurationKeysJson);

        builder.HasIndex(p => p.ApplicationVersionId);
        builder.HasIndex(p => new { p.ApplicationVersionId, p.Key }).IsUnique();
    }
}
