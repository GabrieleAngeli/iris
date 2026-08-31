using Iris.Domain.Tenancy;

namespace Iris.Application.Abstractions;

public interface ICustomerRepository
{
    /// <summary>All customers with their contexts eagerly loaded (read-only).</summary>
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>A single customer with contexts, not change-tracked.</summary>
    Task<Customer?> GetAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>A single customer with contexts, change-tracked for mutation.</summary>
    Task<Customer?> GetForUpdateAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
}
