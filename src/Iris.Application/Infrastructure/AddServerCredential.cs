using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Infrastructure;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Infrastructure;

/// <summary>Command for <c>POST /servers/{serverId}/credentials</c>.</summary>
public sealed record AddServerCredentialCommand(
    Guid ServerId,
    string Username,
    string AuthMethod,
    string SecretValue,
    string? Label);

public sealed class AddServerCredentialHandler(
    IServerRepository servers,
    ISecretStore secretStore,
    IUnitOfWork unitOfWork)
{
    public async Task<ServerCredentialResponse> HandleAsync(
        AddServerCredentialCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Username))
        {
            throw new ValidationException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(command.SecretValue))
        {
            throw new ValidationException("A password or SSH key is required.");
        }

        if (!Enum.TryParse<ServerCredentialAuthMethod>(command.AuthMethod, ignoreCase: true, out var authMethod))
        {
            throw new ValidationException($"Unknown auth method '{command.AuthMethod}'. Expected Password or SshKey.");
        }

        var server = await servers.GetForUpdateAsync(command.ServerId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Server", command.ServerId);

        var credentialId = Guid.CreateVersion7();
        var logicalPath = $"servers/{server.Id}/credentials/{credentialId}";
        var secretReference = await secretStore
            .StoreAsync(logicalPath, command.SecretValue, cancellationToken)
            .ConfigureAwait(false);

        ServerCredential credential;
        try
        {
            credential = server.AddCredential(credentialId, command.Username, authMethod, secretReference, command.Label);
        }
        catch (InvalidOperationException ex)
        {
            // The secret was already stored above — clean it up rather than leaving it orphaned.
            await secretStore.DeleteAsync(secretReference, cancellationToken).ConfigureAwait(false);
            throw new ConflictException(ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return credential.ToResponse();
    }
}
