using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Infrastructure;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Infrastructure;

public sealed record CreateDataServiceCommand(
    string Name,
    string Kind,
    string Endpoint,
    int? Port,
    string? Version,
    string? Size,
    int? StorageGb,
    string Environment,
    string? Username = null,
    string? PasswordValue = null);

public sealed class CreateDataServiceHandler(
    IDataServiceRepository dataServices,
    ISecretStore secretStore,
    IDataServiceInventoryProbe inventoryProbe,
    IUnitOfWork unitOfWork)
{
    public async Task<DataServiceResponse> HandleAsync(
        CreateDataServiceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var parsed = DataServiceParsing.Parse(command.Name, command.Kind, command.Endpoint, command.Port, command.StorageGb, command.Environment);
        if (string.IsNullOrWhiteSpace(command.Username))
        {
            throw new ValidationException("Data service username is required.");
        }

        if (string.IsNullOrWhiteSpace(command.PasswordValue))
        {
            throw new ValidationException("Data service password is required.");
        }

        var id = Guid.CreateVersion7();
        var secretReference = await secretStore
            .StoreAsync($"data-services/{id}/password", command.PasswordValue, cancellationToken)
            .ConfigureAwait(false);

        var instance = new DataServiceInstance(
            id,
            command.Name,
            parsed.Kind,
            command.Endpoint,
            command.Port,
            command.Username,
            secretReference,
            command.Version,
            command.Size,
            command.StorageGb,
            parsed.Environment);

        var snapshot = await inventoryProbe.DiscoverAsync(instance, cancellationToken).ConfigureAwait(false);
        instance.ApplyInventoryDiscovery(snapshot.Kind, snapshot.Version, snapshot.Size, snapshot.StorageGb ?? command.StorageGb);

        await dataServices.AddAsync(instance, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return instance.ToResponse();
    }
}
