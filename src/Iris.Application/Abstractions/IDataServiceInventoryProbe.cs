using Iris.Domain.Infrastructure;

namespace Iris.Application.Abstractions;

public sealed record DataServiceInventorySnapshot(
    DataServiceKind Kind,
    string? Version,
    string? Size,
    int? StorageGb);

public interface IDataServiceInventoryProbe
{
    Task<DataServiceInventorySnapshot> DiscoverAsync(DataServiceInstance dataService, CancellationToken cancellationToken = default);
}
