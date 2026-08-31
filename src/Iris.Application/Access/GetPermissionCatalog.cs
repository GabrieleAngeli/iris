using Iris.Domain.Access;

namespace Iris.Application.Access;

/// <summary>Query for <c>GET /permissions</c>: every permission code Iris recognises.</summary>
public sealed record GetPermissionCatalogQuery;

public sealed class GetPermissionCatalogHandler
{
    public Task<IReadOnlyList<string>> HandleAsync(
        GetPermissionCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<string> catalog = Permissions.All
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(catalog);
    }
}
