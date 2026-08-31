using Iris.Domain.Access;

namespace Iris.Application.Access;

/// <summary>
/// Answers a single authorization question — used by the API authorization layer
/// to back permission-named policies.
/// </summary>
public interface IPermissionAuthorizer
{
    Task<bool> IsAllowedAsync(
        string externalId,
        PermissionId permission,
        AccessScope target,
        CancellationToken cancellationToken = default);
}
