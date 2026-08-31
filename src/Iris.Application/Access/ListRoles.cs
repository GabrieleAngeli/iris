using Iris.Application.Abstractions;
using Iris.Contracts.Access;

namespace Iris.Application.Access;

/// <summary>Query for <c>GET /governance/roles</c>: the role catalog and the permissions each carries.</summary>
public sealed record ListRolesQuery;

public sealed class ListRolesHandler(IRoleRepository roles)
{
    public async Task<IReadOnlyList<RoleResponse>> HandleAsync(
        ListRolesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var all = await roles.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return all
            .Select(r => new RoleResponse(
                r.Key,
                r.Name,
                r.Description,
                r.IsBuiltIn,
                r.Permissions.OrderBy(p => p, StringComparer.Ordinal).ToArray()))
            .ToArray();
    }
}
