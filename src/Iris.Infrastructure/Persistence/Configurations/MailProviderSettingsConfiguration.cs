using Iris.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class MailProviderSettingsConfiguration : IEntityTypeConfiguration<MailProviderSettings>
{
    public void Configure(EntityTypeBuilder<MailProviderSettings> builder)
    {
        builder.ToTable("MailProviderSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.SmtpHost).IsRequired().HasMaxLength(260);
        builder.Property(s => s.SmtpPort).IsRequired();
        builder.Property(s => s.SmtpUsername).HasMaxLength(200);
        builder.Property(s => s.SmtpPasswordSecretReference).HasMaxLength(400);
        builder.Property(s => s.FromAddress).IsRequired().HasMaxLength(320);
        builder.Property(s => s.FromDisplayName).HasMaxLength(200);
        builder.Property(s => s.EnableSsl);

        builder.Property(s => s.CreatedAtUtc);
        builder.Property(s => s.UpdatedAtUtc);
    }
}
