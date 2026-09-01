using Iris.Application.Abstractions;
using Iris.Contracts.Infrastructure;

namespace Iris.Application.Infrastructure;

/// <summary>Query for <c>GET /servers</c>: every registered server and its credentials.</summary>
public sealed record ListServersQuery;

public sealed class ListServersHandler(IServerRepository servers, IUserRepository users)
{
    public async Task<IReadOnlyList<ServerResponse>> HandleAsync(
        ListServersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var allServers = await servers.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var ownerNames = (await users.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(u => u.Id, u => u.DisplayName);

        return allServers
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => s.ToResponse(ownerNames))
            .ToArray();
    }
}
