using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Tenancy;
using Iris.Domain.Tenancy;

namespace Iris.Application.Governance;

/// <summary>Command for <c>PUT /customers/{customerId}</c> — an admin editing a customer's name and active flag.</summary>
public sealed record UpdateCustomerCommand(Guid Id, string Name, bool IsActive);

public sealed class UpdateCustomerHandler(ICustomerRepository customers, IUnitOfWork unitOfWork)
{
    public async Task<CustomerSummaryResponse> HandleAsync(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("Customer name is required.");
        }

        var customer = await customers.GetForUpdateAsync(command.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Customer", command.Id);

        customer.Rename(command.Name);
        if (command.IsActive)
        {
            customer.Activate();
        }
        else
        {
            customer.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Map(customer);
    }

    private static CustomerSummaryResponse Map(Customer customer)
    {
        var contexts = customer.Contexts
            .OrderBy(ctx => ctx.Kind)
            .Select(ctx => new ContextSummaryResponse(ctx.Id, ctx.Name, ctx.Kind.ToString(), ctx.IsActive))
            .ToArray();

        return new CustomerSummaryResponse(customer.Id, customer.Key, customer.Name, customer.IsActive, contexts);
    }
}
