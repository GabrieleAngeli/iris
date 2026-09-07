using Iris.Contracts.Deployments;
using Iris.Domain.Deployments;
using Iris.Domain.Infrastructure;
using Iris.Domain.Tenancy;

namespace Iris.Application.Deployments;

internal static class EnvironmentServerAssignmentMapping
{
    public static EnvironmentServerAssignmentResponse ToResponse(
        this EnvironmentServerAssignment assignment,
        Customer customer,
        CustomerContext context,
        ServerNode server) => new(
            assignment.Id,
            customer.Id,
            customer.Name,
            context.Id,
            context.Name,
            context.Kind.ToString(),
            server.Id,
            server.Name,
            server.HostingType.ToString(),
            assignment.Notes,
            assignment.CreatedAtUtc);
}
