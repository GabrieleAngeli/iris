using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Tenancy;

namespace Iris.Application.Governance;

/// <summary>Command for <c>PUT /customers/{customerId}/contexts/{contextId}/awx-context-name</c>
/// — sets (or, if blank, clears) which context in the AWX automation repo this environment
/// corresponds to, used to resolve Job Templates by name instead of a global id.</summary>
public sealed record SetContextAwxNameCommand(Guid CustomerId, Guid ContextId, string? AwxContextName);

public sealed class SetContextAwxNameHandler(ICustomerRepository customers, IUnitOfWork unitOfWork)
{
    public async Task<ContextSummaryResponse> HandleAsync(
        SetContextAwxNameCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var customer = await customers.GetForUpdateAsync(command.CustomerId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Customer", command.CustomerId);

        var context = customer.Contexts.SingleOrDefault(c => c.Id == command.ContextId)
            ?? throw new NotFoundException("Context", command.ContextId);

        context.SetAwxContextName(command.AwxContextName);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ContextSummaryResponse(context.Id, context.Name, context.Kind.ToString(), context.IsActive, context.AwxContextName);
    }
}
