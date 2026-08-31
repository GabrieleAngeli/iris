using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Iris.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build the model without booting the
/// API host. The provider is chosen by the <c>IRIS_MIGRATIONS_PROVIDER</c> environment
/// variable (<c>Sqlite</c> — default — or <c>Postgres</c>); no live database is needed
/// to add or script migrations.
/// </summary>
public sealed class IrisDbContextFactory : IDesignTimeDbContextFactory<IrisDbContext>
{
    public IrisDbContext CreateDbContext(string[] args)
    {
        var provider = DatabaseProviderParser.Parse(
            Environment.GetEnvironmentVariable("IRIS_MIGRATIONS_PROVIDER"));

        var builder = new DbContextOptionsBuilder<IrisDbContext>();

        if (provider == DatabaseProvider.Postgres)
        {
            builder.UseNpgsql(
                "Host=localhost;Database=iris;Username=iris;Password=iris",
                npgsql => npgsql.MigrationsAssembly(MigrationAssemblies.Postgres));
        }
        else
        {
            builder.UseSqlite(
                "Data Source=iris.design.db",
                sqlite => sqlite.MigrationsAssembly(MigrationAssemblies.Sqlite));
        }

        return new IrisDbContext(builder.Options);
    }
}
