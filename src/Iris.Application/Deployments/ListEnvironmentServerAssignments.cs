using Iris.Application.Abstractions;
using Iris.Contracts.Deployments;

namespace Iris.Application.Deployments;

public sealed record ListEnvironmentServerAssignmentsQuery;

public sealed class ListEnvironmentServerAssignmentsHandler(
    IEnvironmentServerAssignmentRepository assignments,
    ICustomerRepository customers,
    IServerRepository servers)
{
    public async Task<IReadOnlyList<EnvironmentServerAssignmentResponse>> HandleAsync(
        ListEnvironmentServerAssignmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var all = await assignments.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var allCustomers = await customers.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var allServers = await servers.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var contextsById = allCustomers
            .SelectMany(customer => customer.Contexts, (customer, context) => (Customer: customer, Context: context))
            .ToDictionary(pair => pair.Context.Id);

        return all
            .Select(assignment =>
            {
                var (customer, context) = contextsById[assignment.CustomerContextId];
                var server = allServers.Single(s => s.Id == assignment.ServerNodeId);
                return assignment.ToResponse(customer, context, server);
            })
            .OrderBy(response => response.CustomerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(response => response.CustomerContextName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(response => response.ServerName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
