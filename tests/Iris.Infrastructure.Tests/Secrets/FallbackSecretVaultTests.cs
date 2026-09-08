using Iris.Application.Abstractions;
using Iris.Infrastructure.Persistence;
using Iris.Infrastructure.Persistence.Repositories;
using Iris.Infrastructure.Secrets;
using Iris.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Tests.Secrets;

public sealed class FallbackSecretVaultTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"iris-fallback-vault-{Guid.NewGuid():N}.db");
    private readonly IrisDbContext _dbContext;

    public FallbackSecretVaultTests()
    {
        // Pooling=False so the SQLite file handle is actually released on Dispose (SQLite
        // connection pooling otherwise keeps it locked, which broke deleting the temp file below).
        var options = new DbContextOptionsBuilder<IrisDbContext>()
            .UseSqlite($"Data Source={_databasePath};Pooling=False")
            .Options;
        _dbContext = new IrisDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private FallbackSecretVault Vault(EncryptedFallbackSecretStore store) =>
        new(store, new FallbackSecretEntryRepository(_dbContext), new AesGcmSecretProtector(), new FixedClock(DateTimeOffset.UtcNow), new EfUnitOfWork(_dbContext));

    [Fact]
    public async Task UnlockAsync_persists_an_in_memory_only_entry_as_a_new_durable_row()
    {
        var store = new EncryptedFallbackSecretStore();
        await store.StoreAsync("openbao/token", "s.some-token");
        var userId = Guid.CreateVersion7();

        var result = await Vault(store).UnlockAsync(userId, "correct password");

        Assert.Equal(1, result.Persisted);
        Assert.Equal(0, result.Restored);
        Assert.Equal(0, result.OwnershipTransferred);

        var rows = await new FallbackSecretEntryRepository(_dbContext).GetByOwnerAsync(userId);
        Assert.Single(rows);
        Assert.Equal("mock-openbao:openbao/token", rows[0].Reference);
    }

    [Fact]
    public async Task UnlockAsync_restores_a_previously_durable_row_not_yet_in_memory()
    {
        var userId = Guid.CreateVersion7();

        // Simulate a previous process run: persist once, then start with a *fresh* in-memory store.
        var firstRunStore = new EncryptedFallbackSecretStore();
        await firstRunStore.StoreAsync("mail/smtp", "s3cr3t-password");
        await Vault(firstRunStore).UnlockAsync(userId, "correct password");

        var freshStore = new EncryptedFallbackSecretStore();
        var result = await Vault(freshStore).UnlockAsync(userId, "correct password");

        Assert.Equal(0, result.Persisted);
        Assert.Equal(1, result.Restored);
        Assert.Equal("s3cr3t-password", await freshStore.RetrieveAsync("mock-openbao:mail/smtp"));
    }

    [Fact]
    public async Task UnlockAsync_leaves_a_different_owners_row_untouched()
    {
        var ownerA = Guid.CreateVersion7();
        var ownerB = Guid.CreateVersion7();

        var storeA = new EncryptedFallbackSecretStore();
        await storeA.StoreAsync("openbao/token", "owner-a-secret");
        await Vault(storeA).UnlockAsync(ownerA, "password-a");

        var freshStore = new EncryptedFallbackSecretStore();
        var result = await Vault(freshStore).UnlockAsync(ownerB, "password-b");

        Assert.Equal(0, result.Restored);
        Assert.Null(await freshStore.RetrieveAsync("mock-openbao:openbao/token"));
    }

    [Fact]
    public async Task UnlockAsync_reassigns_ownership_when_the_same_reference_is_re_persisted_by_a_different_admin()
    {
        var ownerA = Guid.CreateVersion7();
        var ownerB = Guid.CreateVersion7();

        var storeA = new EncryptedFallbackSecretStore();
        await storeA.StoreAsync("openbao/token", "owner-a-secret");
        await Vault(storeA).UnlockAsync(ownerA, "password-a");

        // Owner B re-saves the same logical secret in a fresh process (before ever unlocking) and
        // then unlocks — this reassigns the row to B, per FallbackSecretVault's documented design.
        var storeB = new EncryptedFallbackSecretStore();
        await storeB.StoreAsync("openbao/token", "owner-b-secret");
        var result = await Vault(storeB).UnlockAsync(ownerB, "password-b");

        Assert.Equal(1, result.Persisted);
        Assert.Equal(1, result.OwnershipTransferred);

        var row = await new FallbackSecretEntryRepository(_dbContext).GetByReferenceAsync("mock-openbao:openbao/token");
        Assert.Equal(ownerB, row!.OwnerUserId);
    }

    [Fact]
    public async Task UnlockAsync_skips_a_row_that_fails_to_decrypt_instead_of_throwing()
    {
        var userId = Guid.CreateVersion7();
        var protector = new AesGcmSecretProtector();
        var repository = new FallbackSecretEntryRepository(_dbContext);

        // A row "encrypted" with the wrong password relative to what Unlock will try — simulates
        // corruption/an impossible-to-decrypt row.
        var corrupted = Domain.Secrets.EncryptedSecretEntry.Create(
            "mock-openbao:corrupted", userId, protector.Protect("value", "a-different-password", "mock-openbao:corrupted"));
        await repository.AddAsync(corrupted);
        await _dbContext.SaveChangesAsync();

        var store = new EncryptedFallbackSecretStore();

        var result = await Vault(store).UnlockAsync(userId, "correct password");

        Assert.Equal(0, result.Restored);
        Assert.Null(await store.RetrieveAsync("mock-openbao:corrupted"));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
