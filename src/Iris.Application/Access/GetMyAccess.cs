using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Access;

namespace Iris.Application.Access;

/// <summary>Query for <c>GET /me</c>: the caller's identity and effective permissions at an optional scope.</summary>
public sealed record GetMyAccessQuery(Guid? CustomerId = null, Guid? ContextId = null);

public sealed class GetMyAccessHandler(
    ICurrentUser currentUser,
    IUserProvisioningService provisioning,
    IUserAccessService accessService)
{
    public async Task<MeResponse> HandleAsync(GetMyAccessQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var user = await provisioning.EnsureProvisionedAsync(currentUser, cancellationToken).ConfigureAwait(false);

        var snapshot = await accessService.GetSnapshotAsync(user.ExternalId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("User snapshot missing immediately after provisioning.");

        var target = ScopeFactory.From(query.CustomerId, query.ContextId);
        var effective = snapshot.EffectivePermissions(target).OrderBy(p => p, StringComparer.Ordinal).ToArray();

        var assignments = snapshot.Assignments
            .Select(a => new RoleAssignmentDto(
                a.RoleKey,
                a.RoleName,
                a.Scope.Type.ToString(),
                a.Scope.CustomerId,
                a.Scope.ContextId,
                a.Permissions.OrderBy(p => p, StringComparer.Ordinal).ToArray()))
            .ToArray();

        return new MeResponse(
            snapshot.UserId,
            snapshot.ExternalId,
            snapshot.Email,
            snapshot.DisplayName,
            target.ToString(),
            effective,
            assignments);
    }
}
