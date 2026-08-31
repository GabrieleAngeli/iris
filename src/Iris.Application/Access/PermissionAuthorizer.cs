using Iris.Domain.Access;

namespace Iris.Application.Access;

internal sealed class PermissionAuthorizer(IUserAccessService accessService) : IPermissionAuthorizer
{
    public async Task<bool> IsAllowedAsync(
        string externalId,
        PermissionId permission,
        AccessScope target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return false;
        }

        var snapshot = await accessService.GetSnapshotAsync(externalId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return false;
        }

        return PermissionResolver.IsAllowed(snapshot.ToGrants(), permission, target);
    }
}
