using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Infrastructure;

namespace Iris.Application.Infrastructure;

/// <summary>Command for <c>POST /servers/{serverId}/credentials</c>.</summary>
public sealed record AddServerCredentialCommand(
    Guid ServerId,
    string Username,
    string AuthMethod,
    string SecretValue,
    string Kind,
    Guid? OwnerUserId,
    string? ServiceName,
    string? Label);

public sealed class AddServerCredentialHandler(
    IServerRepository servers,
    ServerCredentialFactory credentialFactory,
    IUnitOfWork unitOfWork)
{
    public async Task<ServerCredentialResponse> HandleAsync(
        AddServerCredentialCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var server = await servers.GetForUpdateAsync(command.ServerId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Server", command.ServerId);

        var input = new ServerCredentialInput(
            command.Username,
            command.AuthMethod,
            command.SecretValue,
            command.Kind,
            command.OwnerUserId,
            command.ServiceName,
            command.Label);

        var (credential, owner) = await credentialFactory
            .AttachAsync(server, input, cancellationToken)
            .ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return credential.ToResponse(owner?.DisplayName);
    }
}
