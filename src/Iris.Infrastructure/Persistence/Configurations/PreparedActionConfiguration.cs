using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class PreparedActionConfiguration : IEntityTypeConfiguration<PreparedAction>
{
    public void Configure(EntityTypeBuilder<PreparedAction> builder)
    {
        builder.ToTable("PreparedActions");
        builder.HasKey(action => action.Id);
        builder.Property(action => action.Id).ValueGeneratedNever();

        builder.Property(action => action.ApplicationInstallationId).IsRequired();
        builder.Property(action => action.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(action => action.PlanSnapshotJson).IsRequired();
        builder.Property(action => action.ValidationSnapshotJson).IsRequired();
        builder.Property(action => action.RequestedInventory).HasMaxLength(200);
        builder.Property(action => action.RequestedLimit).HasMaxLength(200);
        builder.Property(action => action.CancelReason).HasMaxLength(2000);
        builder.Property(action => action.ExecutedAtUtc);
        builder.Property(action => action.CanceledAtUtc);
        builder.Property(action => action.CreatedAtUtc);
        builder.Property(action => action.UpdatedAtUtc);

        builder.HasIndex(action => action.ApplicationInstallationId);
        builder.HasIndex(action => action.Status);
    }
}
