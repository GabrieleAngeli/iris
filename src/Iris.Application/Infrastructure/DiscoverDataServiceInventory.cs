using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Infrastructure;

namespace Iris.Application.Infrastructure;

public sealed record DiscoverDataServiceInventoryCommand(Guid DataServiceId);

public sealed class DiscoverDataServiceInventoryHandler(
    IDataServiceRepository dataServices,
    IDataServiceInventoryProbe inventoryProbe,
    IUnitOfWork unitOfWork)
{
    public async Task<DataServiceResponse> HandleAsync(
        DiscoverDataServiceInventoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var instance = await dataServices.GetForUpdateAsync(command.DataServiceId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Data service", command.DataServiceId);

        if (string.IsNullOrWhiteSpace(instance.Username) || string.IsNullOrWhiteSpace(instance.PasswordSecretReference))
        {
            throw new ValidationException("Add username and password before discovering the data service.");
        }

        var snapshot = await inventoryProbe.DiscoverAsync(instance, cancellationToken).ConfigureAwait(false);
        instance.ApplyInventoryDiscovery(snapshot.Kind, snapshot.Version, snapshot.Size, snapshot.StorageGb ?? instance.StorageGb);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return instance.ToResponse();
    }
}
