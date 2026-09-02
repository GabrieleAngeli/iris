using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class DependencyDefinitionConfiguration : IEntityTypeConfiguration<DependencyDefinition>
{
    public void Configure(EntityTypeBuilder<DependencyDefinition> builder)
    {
        builder.ToTable("ApplicationDependencies");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.ApplicationVersionId).IsRequired();
        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Category).IsRequired().HasMaxLength(100);
        builder.Property(d => d.Required);
        builder.Property(d => d.Description).HasMaxLength(1000);
        builder.Property(d => d.PlaceholderKey).HasMaxLength(300);

        builder.HasIndex(d => d.ApplicationVersionId);
    }
}
