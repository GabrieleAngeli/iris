using Iris.Domain.Deployments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class EnvironmentServerAssignmentConfiguration : IEntityTypeConfiguration<EnvironmentServerAssignment>
{
    public void Configure(EntityTypeBuilder<EnvironmentServerAssignment> builder)
    {
        builder.ToTable("EnvironmentServerAssignments");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).ValueGeneratedNever();

        builder.Property(assignment => assignment.CustomerContextId).IsRequired();
        builder.Property(assignment => assignment.ServerNodeId).IsRequired();
        builder.Property(assignment => assignment.Notes).HasMaxLength(1000);
        builder.Property(assignment => assignment.CreatedAtUtc);
        builder.Property(assignment => assignment.UpdatedAtUtc);

        builder.HasIndex(assignment => new { assignment.CustomerContextId, assignment.ServerNodeId }).IsUnique();
        builder.HasIndex(assignment => assignment.ServerNodeId);
    }
}
