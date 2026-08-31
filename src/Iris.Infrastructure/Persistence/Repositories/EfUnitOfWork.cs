using Iris.Application.Abstractions;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class EfUnitOfWork(IrisDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
