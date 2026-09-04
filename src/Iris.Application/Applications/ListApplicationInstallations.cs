using Iris.Application.Abstractions;
using Iris.Contracts.Applications;

namespace Iris.Application.Applications;

public sealed record ListApplicationInstallationsQuery;

public sealed class ListApplicationInstallationsHandler(
    IApplicationInstallationRepository installations,
    IApplicationRepository applications,
    IServerRepository servers)
{
    public async Task<IReadOnlyList<ApplicationInstallationResponse>> HandleAsync(
        ListApplicationInstallationsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var allInstallations = await installations.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var allApplications = await applications.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var allServers = await servers.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return allInstallations
            .Select(installation =>
            {
                var application = allApplications.Single(a => a.Id == installation.ApplicationId);
                var version = application.Versions.Single(v => v.Id == installation.ApplicationVersionId);
                var server = allServers.Single(s => s.Id == installation.ServerNodeId);
                return installation.ToResponse(application, version, server);
            })
            .OrderBy(response => response.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(response => response.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
