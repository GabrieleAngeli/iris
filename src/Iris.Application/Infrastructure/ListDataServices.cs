using Iris.Application.Abstractions;
using Iris.Contracts.Infrastructure;

namespace Iris.Application.Infrastructure;

public sealed record ListDataServicesQuery;

public sealed class ListDataServicesHandler(IDataServiceRepository dataServices)
{
    public async Task<IReadOnlyList<DataServiceResponse>> HandleAsync(
        ListDataServicesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var instances = await dataServices.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return instances
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => s.ToResponse())
            .ToArray();
    }
}
