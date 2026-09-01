using Iris.Application.Abstractions;
using Iris.Infrastructure.Invitations;
using Iris.Infrastructure.Persistence;
using Iris.Infrastructure.Persistence.Interceptors;
using Iris.Infrastructure.Persistence.Repositories;
using Iris.Infrastructure.Persistence.Seeding;
using Iris.Infrastructure.Secrets;
using Iris.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Iris.Infrastructure;

/// <summary>
/// Composition entry point for the infrastructure layer: persistence (EF Core /
/// SQLite / PostgreSQL) and, in later increments, the AWX, OpenBao, Ansible and
/// Grafana adapters are wired here against the ports declared in <c>Iris.Application</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIrisInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = DatabaseProviderParser.Parse(configuration["Iris:Database:Provider"]);
        var connectionString = configuration.GetConnectionString("IrisDb")
            ?? throw new InvalidOperationException("Connection string 'IrisDb' is not configured.");
        var migrationsAssembly = MigrationAssemblies.For(provider);

        services.TryAddSingleton<IClock, SystemClock>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<IrisDbContext>((sp, options) =>
        {
            switch (provider)
            {
                case DatabaseProvider.Postgres:
                    options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(migrationsAssembly));
                    break;
                default:
                    options.UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(migrationsAssembly));
                    break;
            }

            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IServerRepository, ServerRepository>();
        services.AddScoped<IUserInvitationRepository, UserInvitationRepository>();
        services.AddScoped<IEditLockRepository, EditLockRepository>();
        services.TryAddSingleton<ISecretStore, InMemorySecretStore>();
        services.TryAddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.TryAddSingleton<IInvitationLinkBuilder, ConfiguredInvitationLinkBuilder>();
        services.TryAddScoped<IInvitationNotifier, LoggingInvitationNotifier>();
        services.AddScoped<IrisDbSeeder>();

        return services;
    }
}
