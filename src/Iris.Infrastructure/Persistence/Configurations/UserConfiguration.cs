using Iris.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.ExternalId).IsRequired().HasMaxLength(200);
        builder.HasIndex(u => u.ExternalId).IsUnique();

        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.IsActive);
        builder.Property(u => u.IsProvisioned).HasDefaultValue(true);

        builder.Property(u => u.PasswordHash).HasMaxLength(200);
        builder.Property(u => u.PasswordUpdatedAtUtc);
        builder.Property(u => u.PasswordSetupPending).HasDefaultValue(false);

        builder.Property(u => u.CreatedAtUtc);
        builder.Property(u => u.UpdatedAtUtc);
    }
}
