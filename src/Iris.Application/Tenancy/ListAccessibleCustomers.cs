using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Contracts.Tenancy;
using Iris.Domain.Tenancy;

namespace Iris.Application.Tenancy;

/// <summary>Query for <c>GET /customers</c>: customers (and contexts) the caller is allowed to see.</summary>
public sealed record ListAccessibleCustomersQuery;

public sealed class ListAccessibleCustomersHandler(
    ICurrentUser currentUser,
    IUserProvisioningService provisioning,
    IUserAccessService accessService,
    ICustomerRepository customers)
{
    public async Task<IReadOnlyList<CustomerSummaryResponse>> HandleAsync(
        ListAccessibleCustomersQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var user = await provisioning.EnsureProvisionedAsync(currentUser, cancellationToken).ConfigureAwait(false);
        var snapshot = await accessService.GetSnapshotAsync(user.ExternalId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("User snapshot missing immediately after provisioning.");

        var all = await customers.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return all
            .Where(c => snapshot.CanSeeCustomer(c.Id))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => Map(c, snapshot))
            .ToArray();
    }

    private static CustomerSummaryResponse Map(Customer customer, UserAccessSnapshot snapshot)
    {
        var contexts = customer.Contexts
            .Where(ctx => snapshot.CanSeeContext(customer.Id, ctx.Id))
            .OrderBy(ctx => ctx.Kind)
            .Select(ctx => new ContextSummaryResponse(ctx.Id, ctx.Name, ctx.Kind.ToString(), ctx.IsActive))
            .ToArray();

        return new CustomerSummaryResponse(customer.Id, customer.Key, customer.Name, customer.IsActive, contexts);
    }
}
