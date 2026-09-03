using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Infrastructure;

namespace Iris.Application.Infrastructure;

public sealed record UpdateDataServiceCommand(
    Guid DataServiceId,
    string Name,
    string Kind,
    string Endpoint,
    int? Port,
    string? Version,
    string? Size,
    int? StorageGb,
    string Environment,
    bool IsActive,
    string? Username = null,
    string? PasswordValue = null);

public sealed class UpdateDataServiceHandler(
    IDataServiceRepository dataServices,
    ISecretStore secretStore,
    IDataServiceInventoryProbe inventoryProbe,
    IUnitOfWork unitOfWork)
{
    public async Task<DataServiceResponse> HandleAsync(
        UpdateDataServiceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var instance = await dataServices.GetForUpdateAsync(command.DataServiceId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Data service", command.DataServiceId);

        var parsed = DataServiceParsing.Parse(
            command.Name,
            command.Kind,
            command.Endpoint,
            command.Port,
            command.StorageGb,
            command.Environment);

        var username = string.IsNullOrWhiteSpace(command.Username) ? instance.Username : command.Username;
        var secretReference = instance.PasswordSecretReference;
        if (!string.IsNullOrEmpty(command.PasswordValue))
        {
            if (!string.IsNullOrEmpty(secretReference))
            {
                await secretStore.DeleteAsync(secretReference, cancellationToken).ConfigureAwait(false);
            }

            secretReference = await secretStore
                .StoreAsync($"data-services/{instance.Id}/password", command.PasswordValue, cancellationToken)
                .ConfigureAwait(false);
        }

        instance.Update(
            command.Name,
            parsed.Kind,
            command.Endpoint,
            command.Port,
            username,
            secretReference,
            command.Version,
            command.Size,
            command.StorageGb,
            parsed.Environment,
            command.IsActive);

        if (!string.IsNullOrWhiteSpace(instance.Username) && !string.IsNullOrWhiteSpace(instance.PasswordSecretReference))
        {
            var snapshot = await inventoryProbe.DiscoverAsync(instance, cancellationToken).ConfigureAwait(false);
            instance.ApplyInventoryDiscovery(snapshot.Kind, snapshot.Version, snapshot.Size, snapshot.StorageGb ?? command.StorageGb);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return instance.ToResponse();
    }
}
