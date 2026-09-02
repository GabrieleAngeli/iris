using Iris.Domain.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iris.Infrastructure.Persistence.Configurations;

internal sealed class DataServiceInstanceConfiguration : IEntityTypeConfiguration<DataServiceInstance>
{
    public void Configure(EntityTypeBuilder<DataServiceInstance> builder)
    {
        builder.ToTable("DataServices");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Kind).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(s => s.Endpoint).IsRequired().HasMaxLength(300);
        builder.Property(s => s.Port);
        builder.Property(s => s.Username).HasMaxLength(200);
        builder.Property(s => s.PasswordSecretReference).HasMaxLength(400);
        builder.Property(s => s.Version).HasMaxLength(80);
        builder.Property(s => s.Size).HasMaxLength(120);
        builder.Property(s => s.StorageGb);
        builder.Property(s => s.Environment).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.IsActive);
        builder.Property(s => s.CreatedAtUtc);
        builder.Property(s => s.UpdatedAtUtc);
    }
}
