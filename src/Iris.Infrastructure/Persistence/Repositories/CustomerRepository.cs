using Iris.Application.Abstractions;
using Iris.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(IrisDbContext dbContext) : ICustomerRepository
{
    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Customers
            .Include(c => c.Contexts)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<Customer?> GetAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        dbContext.Customers
            .Include(c => c.Contexts)
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == customerId, cancellationToken);

    public Task<Customer?> GetForUpdateAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        dbContext.Customers
            .Include(c => c.Contexts)
            .SingleOrDefaultAsync(c => c.Id == customerId, cancellationToken);

    public Task<bool> ExistsByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        dbContext.Customers.AnyAsync(c => c.Key == key, cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default) =>
        await dbContext.Customers.AddAsync(customer, cancellationToken).ConfigureAwait(false);
}
