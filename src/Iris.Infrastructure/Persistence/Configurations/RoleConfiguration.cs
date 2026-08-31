using Iris.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Key).IsRequired().HasMaxLength(100);
        builder.HasIndex(r => r.Key).IsUnique();

        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.IsBuiltIn);

        builder.Property(r => r.CreatedAtUtc);
        builder.Property(r => r.UpdatedAtUtc);

        // Fine-grained permission codes stored as a JSON array in a single column.
        builder.PrimitiveCollection(r => r.Permissions)
            .HasField("_permissions")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("Permissions");
    }
}
