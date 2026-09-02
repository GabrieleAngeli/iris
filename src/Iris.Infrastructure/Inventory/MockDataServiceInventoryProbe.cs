using Iris.Application.Abstractions;
using Iris.Domain.Infrastructure;

namespace Iris.Infrastructure.Inventory;

/// <summary>
/// Deterministic stand-in for the future SQL/Redis probe. It lets the create flow
/// behave like the real one: credentials first, then detected engine metadata.
/// </summary>
public sealed class MockDataServiceInventoryProbe : IDataServiceInventoryProbe
{
    public Task<DataServiceInventorySnapshot> DiscoverAsync(
        DataServiceInstance dataService,
        CancellationToken cancellationToken = default)
    {
        var snapshot = dataService.Kind switch
        {
            DataServiceKind.Mssql => new DataServiceInventorySnapshot(DataServiceKind.Mssql, "SQL Server 2022", "db.m5.large", dataService.StorageGb ?? 100),
            DataServiceKind.PostgreSql => new DataServiceInventorySnapshot(DataServiceKind.PostgreSql, "PostgreSQL 16", "db.t3.medium", dataService.StorageGb ?? 100),
            DataServiceKind.Redis => new DataServiceInventorySnapshot(DataServiceKind.Redis, "Redis 7.2", "cache.t3.small", dataService.StorageGb ?? 20),
            _ => new DataServiceInventorySnapshot(dataService.Kind, null, null, dataService.StorageGb),
        };

        return Task.FromResult(snapshot);
    }
}
