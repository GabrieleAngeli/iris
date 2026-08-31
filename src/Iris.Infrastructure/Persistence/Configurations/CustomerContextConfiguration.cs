using Iris.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class CustomerContextConfiguration : IEntityTypeConfiguration<CustomerContext>
{
    public void Configure(EntityTypeBuilder<CustomerContext> builder)
    {
        builder.ToTable("CustomerContexts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.CustomerId).IsRequired();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(c => c.IsActive);

        builder.Property(c => c.CreatedAtUtc);
        builder.Property(c => c.UpdatedAtUtc);

        builder.HasIndex(c => new { c.CustomerId, c.Name }).IsUnique();
    }
}
