using Iris.Application.Abstractions;
using Iris.Contracts.Applications;
using Iris.Domain.Applications;

namespace Iris.Application.Applications;

public sealed record ListPreparedActionsQuery(
    Guid? CustomerContextId = null,
    Guid? ApplicationId = null,
    Guid? ServerNodeId = null,
    string? Status = null);

/// <summary>
/// The Actions tab: every prepared action, joined with installation/application/server/customer
/// names (same in-memory join pattern as <c>ListApplicationInstallationsHandler</c>) and filterable
/// by customer context, application, server or status. <see cref="ActionSummaryResponse.EffectiveStatus"/>
/// chains a <c>Prepared</c>/<c>Canceled</c> action's own status with its linked
/// <see cref="InstallationRun"/>'s status once executed, so the list reads as one continuous arc.
/// </summary>
public sealed class ListPreparedActionsHandler(
    IPreparedActionRepository actions,
    IApplicationInstallationRepository installations,
    IApplicationRepository applications,
    IServerRepository servers,
    ICustomerRepository customers,
    IInstallationRunRepository runs)
{
    public async Task<IReadOnlyList<ActionSummaryResponse>> HandleAsync(
        ListPreparedActionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var allActions = await actions.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var allInstallations = await installations.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var allApplications = await applications.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var allServers = await servers.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var allCustomers = await customers.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var installationsById = allInstallations.ToDictionary(installation => installation.Id);
        var contextsById = allCustomers
            .SelectMany(customer => customer.Contexts, (customer, context) => (Customer: customer, Context: context))
            .ToDictionary(pair => pair.Context.Id);

        var results = new List<ActionSummaryResponse>();
        foreach (var action in allActions)
        {
            if (!installationsById.TryGetValue(action.ApplicationInstallationId, out var installation))
            {
                continue; // installation deleted since — omit from a list rather than fail the whole page
            }

            if (query.CustomerContextId is { } wantedContext && installation.CustomerContextId != wantedContext)
            {
                continue;
            }

            if (query.ApplicationId is { } wantedApp && installation.ApplicationId != wantedApp)
            {
                continue;
            }

            if (query.ServerNodeId is { } wantedServer && installation.ServerNodeId != wantedServer)
            {
                continue;
            }

            if (!contextsById.TryGetValue(installation.CustomerContextId, out var contextPair))
            {
                continue;
            }

            var application = allApplications.SingleOrDefault(a => a.Id == installation.ApplicationId);
            var version = application?.Versions.SingleOrDefault(v => v.Id == installation.ApplicationVersionId);
            var server = allServers.SingleOrDefault(s => s.Id == installation.ServerNodeId);

            var effectiveStatus = action.Status.ToString();
            if (action.Status == PreparedActionStatus.Executed && action.InstallationRunId is { } runId)
            {
                var run = await runs.GetAsync(runId, cancellationToken).ConfigureAwait(false);
                if (run is not null)
                {
                    effectiveStatus = run.Status.ToString();
                }
            }

            if (!string.IsNullOrWhiteSpace(query.Status) &&
                !string.Equals(effectiveStatus, query.Status, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            results.Add(new ActionSummaryResponse(
                action.Id,
                installation.Id,
                installation.Name,
                application?.Slug ?? "(deleted)",
                version?.Version ?? "?",
                contextPair.Customer.Name,
                contextPair.Context.Kind.ToString(),
                server?.Name ?? "(deleted)",
                effectiveStatus,
                action.CreatedAtUtc,
                action.InstallationRunId));
        }

        return results
            .OrderByDescending(response => response.CreatedAtUtc)
            .ToArray();
    }
}
