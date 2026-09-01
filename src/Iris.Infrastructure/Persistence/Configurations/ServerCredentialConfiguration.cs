using Iris.Domain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class ServerCredentialConfiguration : IEntityTypeConfiguration<ServerCredential>
{
    public void Configure(EntityTypeBuilder<ServerCredential> builder)
    {
        builder.ToTable("ServerCredentials");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.ServerNodeId).IsRequired();
        builder.Property(c => c.Username).IsRequired().HasMaxLength(100);
        builder.Property(c => c.AuthMethod).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.SecretReference).IsRequired().HasMaxLength(400);
        builder.Property(c => c.Label).HasMaxLength(200);

        builder.Property(c => c.CreatedAtUtc);
        builder.Property(c => c.UpdatedAtUtc);

        builder.HasIndex(c => new { c.ServerNodeId, c.Username }).IsUnique();
    }
}
