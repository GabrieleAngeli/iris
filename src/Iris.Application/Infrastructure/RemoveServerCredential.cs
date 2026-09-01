using Iris.Application.Abstractions;
using Iris.Application.Common;

namespace Iris.Application.Infrastructure;

/// <summary>Command for <c>DELETE /servers/{serverId}/credentials/{credentialId}</c>.</summary>
public sealed record RemoveServerCredentialCommand(Guid ServerId, Guid CredentialId);

public sealed class RemoveServerCredentialHandler(
    IServerRepository servers,
    ISecretStore secretStore,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(RemoveServerCredentialCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var server = await servers.GetForUpdateAsync(command.ServerId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Server", command.ServerId);

        var credential = server.Credentials.SingleOrDefault(c => c.Id == command.CredentialId)
            ?? throw new NotFoundException("Server credential", command.CredentialId);

        server.RemoveCredential(credential.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await secretStore.DeleteAsync(credential.SecretReference, cancellationToken).ConfigureAwait(false);
    }
}
