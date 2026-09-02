using Iris.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class TransactionLogEntryConfiguration : IEntityTypeConfiguration<TransactionLogEntry>
{
    public void Configure(EntityTypeBuilder<TransactionLogEntry> builder)
    {
        builder.ToTable("TransactionLog");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.TransactionId).IsRequired();
        builder.Property(e => e.OccurredAtUtc).IsRequired();
        builder.Property(e => e.Area).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Action).IsRequired().HasMaxLength(32);
        builder.Property(e => e.EntityType).IsRequired().HasMaxLength(96);
        builder.Property(e => e.EntityId).IsRequired().HasMaxLength(96);
        builder.Property(e => e.ActorEmail).IsRequired().HasMaxLength(256);
        builder.Property(e => e.ActorDisplayName).IsRequired().HasMaxLength(256);
        builder.Property(e => e.ActorExternalId).HasMaxLength(256);
        builder.Property(e => e.Summary).IsRequired().HasMaxLength(512);

        builder.HasIndex(e => e.TransactionId);
        builder.HasIndex(e => new { e.Area, e.OccurredAtUtc });
        builder.HasIndex(e => new { e.ActorUserId, e.OccurredAtUtc });
    }
}
