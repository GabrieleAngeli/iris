using Iris.Domain.Secrets;

namespace Iris.Domain.Tests.Secrets;

public sealed class EncryptedSecretEntryTests
{
    [Fact]
    public void Create_sets_fields()
    {
        var ownerUserId = Guid.CreateVersion7();

        var entry = EncryptedSecretEntry.Create("mock-openbao:openbao/token", ownerUserId, "aesgcm-pbkdf2sha256$...");

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal("mock-openbao:openbao/token", entry.Reference);
        Assert.Equal(ownerUserId, entry.OwnerUserId);
        Assert.Equal("aesgcm-pbkdf2sha256$...", entry.ProtectedValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_a_blank_reference(string? reference)
    {
        Assert.ThrowsAny<ArgumentException>(() => EncryptedSecretEntry.Create(reference!, Guid.CreateVersion7(), "value"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_a_blank_protected_value(string? protectedValue)
    {
        Assert.ThrowsAny<ArgumentException>(() => EncryptedSecretEntry.Create("ref", Guid.CreateVersion7(), protectedValue!));
    }

    [Fact]
    public void Create_rejects_an_empty_owner_id()
    {
        Assert.Throws<ArgumentException>(() => EncryptedSecretEntry.Create("ref", Guid.Empty, "value"));
    }

    [Fact]
    public void Overwrite_replaces_owner_and_value_and_stamps_the_timestamp()
    {
        var entry = EncryptedSecretEntry.Create("ref", Guid.CreateVersion7(), "old-value");
        var newOwner = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        entry.Overwrite(newOwner, "new-value", now);

        Assert.Equal(newOwner, entry.OwnerUserId);
        Assert.Equal("new-value", entry.ProtectedValue);
        Assert.Equal(now, entry.UpdatedAtUtc);
    }
}
