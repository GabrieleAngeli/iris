using Iris.Application.Abstractions;
using Iris.Contracts.Setup;

namespace Iris.Application.Setup;

/// <summary>Query for <c>GET /setup/status</c>: whether the first-run wizard still needs to run.</summary>
public sealed record GetSetupStatusQuery;

public sealed class GetSetupStatusHandler(IRoleRepository roles, IRoleAssignmentRepository assignments)
{
    /// <summary>The built-in role key that marks someone as a super-admin — see <c>SeedData.BuiltInRoles</c>.</summary>
    public const string PlatformAdminRoleKey = "platform-admin";

    public async Task<SetupStatusResponse> HandleAsync(
        GetSetupStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var needsSetup = !await HasPlatformAdminAsync(roles, assignments, cancellationToken).ConfigureAwait(false);
        return new SetupStatusResponse(needsSetup);
    }

    /// <summary>Shared with <c>CompleteSetupHandler</c>'s own replay guard.</summary>
    internal static async Task<bool> HasPlatformAdminAsync(
        IRoleRepository roles,
        IRoleAssignmentRepository assignments,
        CancellationToken cancellationToken)
    {
        var platformAdminRole = await roles.GetByKeyAsync(PlatformAdminRoleKey, cancellationToken).ConfigureAwait(false);
        if (platformAdminRole is null)
        {
            // The built-in role catalog is seeded unconditionally at startup — this would mean
            // the seeder hasn't run yet, not that setup is somehow already done.
            return false;
        }

        return await assignments.ExistsForRoleAsync(platformAdminRole.Id, cancellationToken).ConfigureAwait(false);
    }
}
