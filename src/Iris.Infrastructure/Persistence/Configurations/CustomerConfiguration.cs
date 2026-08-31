using Iris.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Key).IsRequired().HasMaxLength(100);
        builder.HasIndex(c => c.Key).IsUnique();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.IsActive);

        builder.Property(c => c.CreatedAtUtc);
        builder.Property(c => c.UpdatedAtUtc);

        builder.HasMany(c => c.Contexts)
            .WithOne()
            .HasForeignKey(ctx => ctx.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Customer.Contexts))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
