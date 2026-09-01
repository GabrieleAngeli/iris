using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class PlaceholderDefinitionConfiguration : IEntityTypeConfiguration<PlaceholderDefinition>
{
    public void Configure(EntityTypeBuilder<PlaceholderDefinition> builder)
    {
        builder.ToTable("ApplicationPlaceholders");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.ApplicationVersionId).IsRequired();
        builder.Property(p => p.Key).IsRequired().HasMaxLength(300);
        builder.Property(p => p.Category).HasMaxLength(100);
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.Required);

        builder.HasIndex(p => p.ApplicationVersionId);
    }
}
