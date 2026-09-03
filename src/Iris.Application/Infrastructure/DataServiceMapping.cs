using Iris.Contracts.Infrastructure;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Infrastructure;

internal static class DataServiceMapping
{
    public static DataServiceResponse ToResponse(this DataServiceInstance instance) => new(
        instance.Id,
        instance.Name,
        instance.Kind.ToString(),
        instance.Endpoint,
        instance.Port,
        instance.Username,
        instance.Version,
        instance.Size,
        instance.StorageGb,
        instance.Environment.ToString(),
        instance.IsActive);
}
