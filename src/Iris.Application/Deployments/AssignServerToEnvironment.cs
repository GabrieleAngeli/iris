using Iris.Application.Abstractions;
using Iris.Application.Applications;
using Iris.Application.Common;
using Iris.Contracts.Deployments;
using Iris.Domain.Deployments;

namespace Iris.Application.Deployments;

public sealed record AssignServerToEnvironmentCommand(Guid CustomerContextId, Guid ServerNodeId, string? Notes);

public sealed class AssignServerToEnvironmentHandler(
    ICustomerRepository customers,
    IServerRepository servers,
    IEnvironmentServerAssignmentRepository assignments,
    IUnitOfWork unitOfWork)
{
    public async Task<EnvironmentServerAssignmentResponse> HandleAsync(
        AssignServerToEnvironmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (customer, context) = await customers.ResolveCustomerContextAsync(command.CustomerContextId, cancellationToken).ConfigureAwait(false);
        var server = await servers.GetAsync(command.ServerNodeId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Server", command.ServerNodeId);

        if (await assignments.ExistsAsync(context.Id, server.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"Server '{server.Name}' is already assigned to '{customer.Name} - {context.Name}'.");
        }

        var assignment = new EnvironmentServerAssignment(Guid.CreateVersion7(), context.Id, server.Id, command.Notes);
        await assignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return assignment.ToResponse(customer, context, server);
    }
}
