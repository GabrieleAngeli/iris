using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Tenancy;
using Iris.Domain.Tenancy;

namespace Iris.Application.Governance;

/// <summary>Command for <c>POST /customers/{customerId}/contexts</c>.</summary>
public sealed record AddContextCommand(Guid CustomerId, string Name, string Kind);

public sealed class AddContextHandler(ICustomerRepository customers, IUnitOfWork unitOfWork)
{
    public async Task<ContextSummaryResponse> HandleAsync(
        AddContextCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("Context name is required.");
        }

        if (!Enum.TryParse<ContextKind>(command.Kind, ignoreCase: true, out var kind))
        {
            throw new ValidationException($"Unknown context kind '{command.Kind}'. Expected Test, Staging or Production.");
        }

        var customer = await customers.GetForUpdateAsync(command.CustomerId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Customer", command.CustomerId);

        CustomerContext context;
        try
        {
            context = customer.AddContext(Guid.CreateVersion7(), command.Name, kind);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ContextSummaryResponse(context.Id, context.Name, context.Kind.ToString(), context.IsActive);
    }
}
