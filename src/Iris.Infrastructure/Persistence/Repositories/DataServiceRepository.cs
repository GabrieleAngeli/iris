using Iris.Application.Abstractions;
using Iris.Domain.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence.Repositories;

internal sealed class DataServiceRepository(IrisDbContext dbContext) : IDataServiceRepository
{
    public async Task<IReadOnlyList<DataServiceInstance>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.DataServices
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<DataServiceInstance?> GetForUpdateAsync(Guid dataServiceId, CancellationToken cancellationToken = default) =>
        dbContext.DataServices.SingleOrDefaultAsync(s => s.Id == dataServiceId, cancellationToken);

    public async Task AddAsync(DataServiceInstance instance, CancellationToken cancellationToken = default) =>
        await dbContext.DataServices.AddAsync(instance, cancellationToken).ConfigureAwait(false);
}
