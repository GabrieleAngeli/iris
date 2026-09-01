using Iris.Application.Abstractions;
using Iris.Contracts.Applications;

namespace Iris.Application.Applications;

/// <summary>Query for <c>GET /applications</c>: the catalog, with each version's knowledge in summary form.</summary>
public sealed record ListApplicationsQuery;

public sealed class ListApplicationsHandler(IApplicationRepository applications)
{
    public async Task<IReadOnlyList<ApplicationResponse>> HandleAsync(
        ListApplicationsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var all = await applications.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return all
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => a.ToResponse())
            .ToArray();
    }
}
