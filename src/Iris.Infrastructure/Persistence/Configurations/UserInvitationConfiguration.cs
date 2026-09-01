using Iris.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.ToTable("UserInvitations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.UserId).IsRequired();
        builder.Property(i => i.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(i => i.IssuedByUserId);
        builder.Property(i => i.ExpiresAtUtc).IsRequired();
        builder.Property(i => i.ConsumedAtUtc);

        builder.Property(i => i.CreatedAtUtc);
        builder.Property(i => i.UpdatedAtUtc);

        builder.HasIndex(i => i.UserId);
        builder.HasIndex(i => i.TokenHash).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
