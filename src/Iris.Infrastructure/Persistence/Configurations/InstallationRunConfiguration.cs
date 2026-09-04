using Iris.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class InstallationRunConfiguration : IEntityTypeConfiguration<InstallationRun>
{
    public void Configure(EntityTypeBuilder<InstallationRun> builder)
    {
        builder.ToTable("InstallationRuns");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id).ValueGeneratedNever();

        builder.Property(run => run.ApplicationInstallationId).IsRequired();
        builder.Property(run => run.Kind).HasConversion<string>().HasMaxLength(40);
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(run => run.ExternalJobId).HasMaxLength(100);
        builder.Property(run => run.ExternalUrl).HasMaxLength(500);
        builder.Property(run => run.SubmittedVariablesJson);
        builder.Property(run => run.Message).HasMaxLength(2000);
        builder.Property(run => run.CompletedAtUtc);
        builder.Property(run => run.CreatedAtUtc);
        builder.Property(run => run.UpdatedAtUtc);

        builder.HasIndex(run => run.ApplicationInstallationId);
    }
}
