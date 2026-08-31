using Iris.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Iris.Infrastructure.Persistence;

/// <summary>Applies pending migrations and runs the seeder. Call once at startup.</summary>
public static class IrisDbInitializer
{
    public static async Task MigrateAndSeedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<IrisDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IrisDbInitializer));

        logger.LogInformation("Applying database migrations…");
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        var seeder = provider.GetRequiredService<IrisDbSeeder>();
        await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Database is ready.");
    }
}
