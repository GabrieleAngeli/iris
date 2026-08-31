using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Tenancy;
using Iris.Domain.Tenancy;

namespace Iris.Application.Governance;

/// <summary>Command for <c>POST /customers</c>.</summary>
public sealed record CreateCustomerCommand(string Key, string Name);

public sealed class CreateCustomerHandler(ICustomerRepository customers, IUnitOfWork unitOfWork)
{
    public async Task<CustomerSummaryResponse> HandleAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Key) || string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("Customer key and name are required.");
        }

        var key = command.Key.Trim().ToLowerInvariant();
        if (await customers.ExistsByKeyAsync(key, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"A customer with key '{key}' already exists.");
        }

        var customer = new Customer(Guid.CreateVersion7(), key, command.Name);
        await customers.AddAsync(customer, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CustomerSummaryResponse(customer.Id, customer.Key, customer.Name, customer.IsActive, []);
    }
}
