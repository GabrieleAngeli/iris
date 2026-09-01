using Iris.Domain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class ServerNodeConfiguration : IEntityTypeConfiguration<ServerNode>
{
    public void Configure(EntityTypeBuilder<ServerNode> builder)
    {
        builder.ToTable("Servers");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Hostname).HasMaxLength(260);

        builder.Property(s => s.Os).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.HostingType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Environment).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(s => s.PublicIpAddress).HasMaxLength(45);
        builder.Property(s => s.PrivateIpAddress).HasMaxLength(45);
        builder.Property(s => s.IsActive);

        builder.Property(s => s.CreatedAtUtc);
        builder.Property(s => s.UpdatedAtUtc);

        builder.HasMany(s => s.Credentials)
            .WithOne()
            .HasForeignKey(c => c.ServerNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(ServerNode.Credentials))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
