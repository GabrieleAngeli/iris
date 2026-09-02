using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Infrastructure;
using Iris.Domain.Infrastructure;
using Iris.Domain.Tenancy;

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

internal static class DataServiceParsing
{
    public static (DataServiceKind Kind, ContextKind Environment) Parse(
        string name,
        string kind,
        string endpoint,
        int? port,
        int? storageGb,
        string environment)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Data service name is required.");
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ValidationException("Data service endpoint is required.");
        }

        if (!Enum.TryParse<DataServiceKind>(kind, ignoreCase: true, out var parsedKind))
        {
            throw new ValidationException("Unknown data service kind. Expected Mssql, PostgreSql or Redis.");
        }

        if (port is < 1 or > 65535)
        {
            throw new ValidationException("Port must be between 1 and 65535.");
        }

        if (storageGb < 0)
        {
            throw new ValidationException("Storage GB cannot be negative.");
        }

        if (!Enum.TryParse<ContextKind>(environment, ignoreCase: true, out var parsedEnvironment))
        {
            throw new ValidationException("Unknown environment. Expected Test, Staging or Production.");
        }

        return (parsedKind, parsedEnvironment);
    }
}
