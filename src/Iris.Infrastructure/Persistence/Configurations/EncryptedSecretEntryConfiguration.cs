using Iris.Domain.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class EncryptedSecretEntryConfiguration : IEntityTypeConfiguration<EncryptedSecretEntry>
{
    public void Configure(EntityTypeBuilder<EncryptedSecretEntry> builder)
    {
        builder.ToTable("EncryptedSecretEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Reference).IsRequired().HasMaxLength(400);
        // No HasMaxLength on ProtectedValue — ciphertext length tracks the plaintext's (an
        // encrypted SSH private key can be long), same reasoning as SecretReference elsewhere
        // being the only length-capped part of a secret.
        builder.Property(e => e.ProtectedValue).IsRequired();

        builder.Property(e => e.CreatedAtUtc);
        builder.Property(e => e.UpdatedAtUtc);

        builder.HasIndex(e => e.Reference).IsUnique();

        // OwnerUserId references Users.Id but is kept as a plain indexed column, not a DB
        // foreign key — same convention/rationale as ServerCredential.OwnerUserId.
        builder.Property(e => e.OwnerUserId).IsRequired();
        builder.HasIndex(e => e.OwnerUserId);
    }
}
