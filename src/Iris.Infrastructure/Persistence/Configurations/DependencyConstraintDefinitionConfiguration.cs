using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class DependencyConstraintDefinitionConfiguration : IEntityTypeConfiguration<DependencyConstraintDefinition>
{
    public void Configure(EntityTypeBuilder<DependencyConstraintDefinition> builder)
    {
        builder.ToTable("ApplicationDependencyConstraints");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.ApplicationVersionId).IsRequired();
        builder.Property(c => c.PlaceholderKey).HasMaxLength(300);
        builder.Property(c => c.ServiceKind).HasMaxLength(100);
        builder.Property(c => c.VersionExpression).HasMaxLength(200);
        builder.Property(c => c.DetailsJson);

        builder.HasIndex(c => c.ApplicationVersionId);
    }
}
