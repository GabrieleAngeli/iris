using Iris.Domain.Infrastructure;

namespace Iris.Application.Abstractions;

public interface IDataServiceRepository
{
    Task<IReadOnlyList<DataServiceInstance>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DataServiceInstance?> GetForUpdateAsync(Guid dataServiceId, CancellationToken cancellationToken = default);

    Task AddAsync(DataServiceInstance instance, CancellationToken cancellationToken = default);
}
