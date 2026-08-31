using Iris.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("RoleAssignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.RoleId).IsRequired();

        builder.ComplexProperty(a => a.Scope, scope =>
        {
            scope.Property(s => s.Type)
                .HasConversion<string>()
                .HasColumnName("ScopeType")
                .HasMaxLength(20)
                .IsRequired();
            scope.Property(s => s.CustomerId).HasColumnName("ScopeCustomerId");
            scope.Property(s => s.ContextId).HasColumnName("ScopeContextId");
        });

        builder.Property(a => a.CreatedAtUtc);
        builder.Property(a => a.UpdatedAtUtc);

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => new { a.UserId, a.RoleId });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(a => a.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
