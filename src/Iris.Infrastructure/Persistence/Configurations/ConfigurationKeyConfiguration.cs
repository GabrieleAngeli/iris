using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class ConfigurationKeyConfiguration : IEntityTypeConfiguration<ConfigurationKey>
{
    public void Configure(EntityTypeBuilder<ConfigurationKey> builder)
    {
        builder.ToTable("ApplicationConfigurationKeys");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();

        builder.Property(k => k.ApplicationVersionId).IsRequired();
        builder.Property(k => k.Key).IsRequired().HasMaxLength(300);
        builder.Property(k => k.TargetKind).IsRequired().HasMaxLength(100);
        builder.Property(k => k.Required);
        builder.Property(k => k.Secret);
        builder.Property(k => k.DefaultValue).HasMaxLength(1000);
        builder.Property(k => k.Description).HasMaxLength(1000);
        builder.Property(k => k.Purpose).HasMaxLength(200);
        builder.Property(k => k.PlaceholderKey).HasMaxLength(300);
        builder.Property(k => k.ValueType).HasMaxLength(80);
        builder.Property(k => k.ItemType).HasMaxLength(80);
        builder.Property(k => k.Scope).HasMaxLength(100);
        builder.Property(k => k.SerializationJson);
        builder.Property(k => k.ResolutionJson);
        builder.Property(k => k.ProfilesJson);
        builder.Property(k => k.ProfileDefaultsJson);
        builder.Property(k => k.ItemSchemaJson);

        builder.HasIndex(k => k.ApplicationVersionId);
    }
}
