using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Infrastructure;
using Iris.Domain.Infrastructure;
using Iris.Domain.Tenancy;

namespace Iris.Application.Infrastructure;

/// <summary>Command for <c>POST /servers</c>.</summary>
public sealed record CreateServerCommand(
    string Name,
    string? Hostname,
    string Os,
    string HostingType,
    string? PublicIpAddress,
    string? PrivateIpAddress,
    string Environment);

public sealed class CreateServerHandler(IServerRepository servers, IUnitOfWork unitOfWork)
{
    public async Task<ServerResponse> HandleAsync(CreateServerCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("Server name is required.");
        }

        if (!Enum.TryParse<ServerOs>(command.Os, ignoreCase: true, out var os))
        {
            throw new ValidationException($"Unknown OS '{command.Os}'. Expected Linux or Windows.");
        }

        if (!Enum.TryParse<ServerHostingType>(command.HostingType, ignoreCase: true, out var hostingType))
        {
            throw new ValidationException($"Unknown hosting type '{command.HostingType}'. Expected SelfHosted or Cloud.");
        }

        if (!Enum.TryParse<ContextKind>(command.Environment, ignoreCase: true, out var environment))
        {
            throw new ValidationException($"Unknown environment '{command.Environment}'. Expected Test, Staging or Production.");
        }

        if (string.IsNullOrWhiteSpace(command.PublicIpAddress) && string.IsNullOrWhiteSpace(command.PrivateIpAddress))
        {
            throw new ValidationException("A server needs at least a public or a private IP address.");
        }

        var server = new ServerNode(
            Guid.CreateVersion7(),
            command.Name,
            command.Hostname,
            os,
            hostingType,
            command.PublicIpAddress,
            command.PrivateIpAddress,
            environment);

        await servers.AddAsync(server, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return server.ToResponse();
    }
}
