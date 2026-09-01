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
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.OwnerUserId);
        builder.Property(c => c.ServiceName).HasMaxLength(100);
        builder.Property(c => c.Label).HasMaxLength(200);

        builder.Property(c => c.CreatedAtUtc);
        builder.Property(c => c.UpdatedAtUtc);

        builder.HasIndex(c => new { c.ServerNodeId, c.Username }).IsUnique();

        // OwnerUserId references Users.Id but is kept as a plain indexed column, not a DB
        // foreign key: the application already guarantees the user exists on write, and this
        // avoids a SQLite table rebuild on migration. (There is no delete-user flow yet.)
        builder.HasIndex(c => c.OwnerUserId);
    }
}
