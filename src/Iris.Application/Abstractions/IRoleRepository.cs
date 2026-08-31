using Iris.Domain.Access;

namespace Iris.Application.Abstractions;

public interface IRoleRepository
{
    Task<IReadOnlyList<Role>> GetByIdsAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Role?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
}
