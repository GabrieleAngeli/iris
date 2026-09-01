using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationDefinitionConfiguration : IEntityTypeConfiguration<ApplicationDefinition>
{
    public void Configure(EntityTypeBuilder<ApplicationDefinition> builder)
    {
        builder.ToTable("Applications");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);

        builder.Property(a => a.Slug).IsRequired().HasMaxLength(100);
        builder.HasIndex(a => a.Slug).IsUnique();

        builder.Property(a => a.RuntimeType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.RepositoryUrl).IsRequired().HasMaxLength(500);
        builder.Property(a => a.DefaultBranch).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Description).HasMaxLength(1000);
        builder.Property(a => a.IsActive);

        builder.Property(a => a.CreatedAtUtc);
        builder.Property(a => a.UpdatedAtUtc);

        builder.HasMany(a => a.Versions)
            .WithOne()
            .HasForeignKey(v => v.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(ApplicationDefinition.Versions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
