using Iris.Application.Abstractions;
using Iris.Application.Common;

namespace Iris.Application.Infrastructure;

/// <summary>Command for <c>DELETE /servers/{id}</c> — removes the server, its credentials and their secrets.</summary>
public sealed record DeleteServerCommand(Guid Id);

public sealed class DeleteServerHandler(IServerRepository servers, ISecretStore secretStore, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(DeleteServerCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var server = await servers.GetForUpdateAsync(command.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Server", command.Id);

        var secretReferences = server.Credentials.Select(c => c.SecretReference).ToArray();

        servers.Remove(server);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var reference in secretReferences)
        {
            await secretStore.DeleteAsync(reference, cancellationToken).ConfigureAwait(false);
        }
    }
}
