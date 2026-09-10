using Iris.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class IntegrationSettingsConfiguration : IEntityTypeConfiguration<IntegrationSettings>
{
    public void Configure(EntityTypeBuilder<IntegrationSettings> builder)
    {
        builder.ToTable("IntegrationSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.OpenBaoEndpoint).HasMaxLength(500);
        builder.Property(s => s.OpenBaoTokenSecretReference).HasMaxLength(400);
        builder.Property(s => s.OpenBaoMountPath).IsRequired().HasMaxLength(100);
        builder.Property(s => s.OpenBaoUseKvV2);

        builder.Property(s => s.AwxEndpoint).HasMaxLength(500);
        builder.Property(s => s.AwxTokenSecretReference).HasMaxLength(400);
        builder.Property(s => s.AwxJobTemplateId);
        builder.Property(s => s.AwxOAuthClientId).HasMaxLength(400);
        builder.Property(s => s.AwxOAuthClientSecretReference).HasMaxLength(400);
        builder.Property(s => s.AwxRefreshTokenSecretReference).HasMaxLength(400);

        builder.Property(s => s.AnsibleEndpoint).HasMaxLength(500);
        builder.Property(s => s.AnsiblePlaybook).IsRequired().HasMaxLength(260);
        builder.Property(s => s.AnsibleInventory).HasMaxLength(260);

        builder.Property(s => s.AzureDevOpsEndpoint).HasMaxLength(500);
        builder.Property(s => s.AzureDevOpsTokenSecretReference).HasMaxLength(400);

        builder.Property(s => s.NexusEndpoint).HasMaxLength(500);
        builder.Property(s => s.NexusTokenSecretReference).HasMaxLength(400);

        builder.Property(s => s.CreatedAtUtc);
        builder.Property(s => s.UpdatedAtUtc);
    }
}
