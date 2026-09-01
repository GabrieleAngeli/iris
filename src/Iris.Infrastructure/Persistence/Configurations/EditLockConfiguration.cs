using Iris.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class EditLockConfiguration : IEntityTypeConfiguration<EditLock>
{
    public void Configure(EntityTypeBuilder<EditLock> builder)
    {
        builder.ToTable("EditLocks");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.ResourceType).IsRequired().HasMaxLength(40);
        builder.Property(l => l.ResourceId).IsRequired();
        builder.Property(l => l.HolderUserId).IsRequired();
        builder.Property(l => l.HolderDisplayName).IsRequired().HasMaxLength(200);
        builder.Property(l => l.AcquiredAtUtc).IsRequired();
        builder.Property(l => l.RefreshedAtUtc).IsRequired();
        builder.Property(l => l.ExpiresAtUtc).IsRequired();

        // One lock per resource — the guard behind the acquire race.
        builder.HasIndex(l => new { l.ResourceType, l.ResourceId }).IsUnique();
    }
}
