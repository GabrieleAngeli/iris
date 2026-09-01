using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Domain.Access;
using Iris.Domain.Infrastructure;

namespace Iris.Application.Infrastructure;

/// <summary>The credential fields an operator supplies for a server (write side).</summary>
public sealed record ServerCredentialInput(
    string Username,
    string AuthMethod,
    string SecretValue,
    string Kind,
    Guid? OwnerUserId,
    string? ServiceName,
    string? Label);

/// <summary>
/// Shared credential-creation used by both <see cref="CreateServerHandler"/> (initial credential) and
/// <see cref="AddServerCredentialHandler"/>: validates the inputs, resolves the owner, stores the secret
/// out-of-band, and attaches a <see cref="ServerCredential"/> to the server aggregate.
/// </summary>
public sealed class ServerCredentialFactory(ISecretStore secretStore, IUserRepository users)
{
    public async Task<(ServerCredential Credential, User? Owner)> AttachAsync(
        ServerNode server,
        ServerCredentialInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.Username))
        {
            throw new ValidationException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(input.SecretValue))
        {
            throw new ValidationException("A password or SSH key is required.");
        }

        if (!Enum.TryParse<ServerCredentialAuthMethod>(input.AuthMethod, ignoreCase: true, out var authMethod))
        {
            throw new ValidationException($"Unknown auth method '{input.AuthMethod}'. Expected Password or SshKey.");
        }

        if (!Enum.TryParse<ServerCredentialKind>(input.Kind, ignoreCase: true, out var kind))
        {
            throw new ValidationException($"Unknown credential kind '{input.Kind}'. Expected SystemUser or ServiceAccount.");
        }

        User? owner = null;
        Guid? ownerUserId = null;
        string? serviceName = null;

        switch (kind)
        {
            case ServerCredentialKind.SystemUser:
                if (!string.IsNullOrWhiteSpace(input.ServiceName))
                {
                    throw new ValidationException("A system-user credential must not carry a service name.");
                }

                if (input.OwnerUserId is { } id && id != Guid.Empty)
                {
                    owner = await users.GetAsync(id, cancellationToken).ConfigureAwait(false)
                        ?? throw new NotFoundException("User", id);
                    ownerUserId = owner.Id;
                }

                break;

            case ServerCredentialKind.ServiceAccount:
                if (string.IsNullOrWhiteSpace(input.ServiceName))
                {
                    throw new ValidationException("A service-account credential requires a service name (e.g. 'ansible').");
                }

                if (input.OwnerUserId is not null)
                {
                    throw new ValidationException("A service-account credential cannot be linked to an Iris user.");
                }

                serviceName = input.ServiceName.Trim();
                break;
        }

        var credentialId = Guid.CreateVersion7();
        var logicalPath = $"servers/{server.Id}/credentials/{credentialId}";
        var secretReference = await secretStore
            .StoreAsync(logicalPath, input.SecretValue, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var credential = server.AddCredential(
                credentialId, input.Username, authMethod, secretReference, kind, ownerUserId, serviceName, input.Label);
            return (credential, owner);
        }
        catch (InvalidOperationException ex)
        {
            // The secret was stored above — don't leave it orphaned when the aggregate rejects the credential.
            await secretStore.DeleteAsync(secretReference, cancellationToken).ConfigureAwait(false);
            throw new ConflictException(ex.Message);
        }
        catch (ArgumentException ex)
        {
            await secretStore.DeleteAsync(secretReference, cancellationToken).ConfigureAwait(false);
            throw new ValidationException(ex.Message);
        }
    }
}
