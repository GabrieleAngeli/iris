namespace Iris.Application.Abstractions;

public sealed record IntegrationConnectorStatus(
    string Key,
    string Name,
    string Status,
    string? Endpoint,
    string? Message = null);

public interface IIntegrationConnector
{
    string Key { get; }

    string Name { get; }

    string? Endpoint { get; }

    Task<IntegrationConnectorStatus> GetStatusAsync(
        bool probe = false,
        CancellationToken cancellationToken = default);
}
