using Iris.Application.Abstractions;
using Iris.Domain.Infrastructure;
using Iris.Domain.Settings;
using Iris.Infrastructure.Containers;
using Iris.Infrastructure.Invitations;
using Iris.Infrastructure.Inventory;
using Iris.Infrastructure.Integrations;
using Iris.Infrastructure.Mail;
using Iris.Infrastructure.Persistence;
using Iris.Infrastructure.Processes;
using Iris.Infrastructure.Persistence.Interceptors;
using Iris.Infrastructure.Remote;
using Iris.Infrastructure.Persistence.Repositories;
using Iris.Infrastructure.Persistence.Seeding;
using Iris.Infrastructure.Secrets;
using Iris.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
        services.AddScoped<TransactionLogInterceptor>();

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

            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<TransactionLogInterceptor>());
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IServerRepository, ServerRepository>();
        services.AddScoped<IDataServiceRepository, DataServiceRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IApplicationInstallationRepository, ApplicationInstallationRepository>();
        services.AddScoped<IInstallationRunRepository, InstallationRunRepository>();
        services.AddScoped<IPreparedActionRepository, PreparedActionRepository>();
        services.AddScoped<IEnvironmentServerAssignmentRepository, EnvironmentServerAssignmentRepository>();
        services.AddScoped<IUserInvitationRepository, UserInvitationRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IEditLockRepository, EditLockRepository>();
        services.AddScoped<IMailProviderSettingsRepository, MailProviderSettingsRepository>();
        services.AddScoped<IIntegrationSettingsRepository, IntegrationSettingsRepository>();
        services.AddScoped<IFallbackSecretEntryRepository, FallbackSecretEntryRepository>();
        services.AddScoped<ITransactionLogRepository, TransactionLogRepository>();
        services.TryAddScoped<IServerInventoryProbe, AwxServerInventoryProbe>();
        services.TryAddSingleton<IProcessRunner, SystemProcessRunner>();
        services.TryAddScoped<IContainerRuntime, DockerCliContainerRuntime>();
        services.TryAddScoped<IDataServiceInventoryProbe, MockDataServiceInventoryProbe>();
        RegisterIntegrations(services, configuration, provider, connectionString, migrationsAssembly);
        services.TryAddSingleton<IIntegrationHealthMonitor, IntegrationHealthMonitor>();
        services.TryAddSingleton<IIntegrationHealthChecker, IntegrationHealthChecker>();
        services.TryAddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.TryAddSingleton<AesGcmSecretProtector>();
        services.TryAddSingleton<IInvitationLinkBuilder, ConfiguredInvitationLinkBuilder>();
        services.TryAddScoped<IEmailSender, SmtpEmailSender>();
        services.TryAddScoped<IInvitationNotifier, SmtpInvitationNotifier>();
        services.AddScoped<IrisDbSeeder>();

        return services;
    }

    private static void RegisterIntegrations(
        IServiceCollection services,
        IConfiguration configuration,
        DatabaseProvider provider,
        string connectionString,
        string migrationsAssembly)
    {
        var integrations = configuration.GetSection("Iris:Integrations");

        // Persisted settings (saved via PUT /system/integrations/*) override appsettings/env
        // config, group by group, for this boot. Best-effort: on a genuinely fresh database
        // the table may not be migrated yet (migrations run later, after Build() — see
        // IrisDbInitializer, called from Program.cs), and the DB itself may not even be
        // reachable yet in a container-orchestrated startup. Either case falls back to the
        // IConfiguration-only values below; startup must never fail because of this.
        var persisted = TryReadPersistedIntegrationSettings(provider, connectionString, migrationsAssembly);

        var openBao = IntegrationOptionsFactory.BuildOpenBao(persisted, integrations);
        var ansible = IntegrationOptionsFactory.BuildAnsible(persisted, integrations);
        var awx = IntegrationOptionsFactory.BuildAwx(persisted, integrations);
        var azureDevOps = IntegrationOptionsFactory.BuildAzureDevOps(persisted, integrations, openBao);
        var opsHost = IntegrationOptionsFactory.BuildOpsHost(persisted, integrations);
        var nexus = IntegrationOptionsFactory.BuildNexus(persisted, integrations, openBao);

        // These are registered as already-constructed instances (not factories) — every consumer
        // below shares the exact same reference. IIntegrationSettingsReloader relies on this: it
        // mutates these same objects' properties in place after a save, so a changed setting takes
        // effect immediately, with no restart and no change needed to any connector class.
        services.AddSingleton(openBao);
        services.AddSingleton(ansible);
        services.AddSingleton(awx);
        services.AddSingleton(azureDevOps);
        services.AddSingleton(nexus);
        services.AddSingleton(opsHost);

        services.AddSingleton<OpenBaoConnector>();
        services.AddSingleton<IIntegrationConnector>(sp => sp.GetRequiredService<OpenBaoConnector>());

        // One switchable ISecretStore: it starts on the encrypted fallback vault (or straight on
        // OpenBao if a token was given via config/env) and can be flipped to real OpenBao at
        // runtime once its token is unlocked — see SwitchableSecretStore / ISecretStorePromotion.
        // FallbackSecretVault is always registered: when OpenBao is the live store its cache is
        // empty, so it reports nothing to unlock and UnlockAsync is a no-op.
        services.AddSingleton<EncryptedFallbackSecretStore>();
        services.AddSingleton<IFallbackSecretCache>(sp => sp.GetRequiredService<EncryptedFallbackSecretStore>());
        services.AddSingleton<SwitchableSecretStore>();
        services.AddSingleton<ISecretStore>(sp => sp.GetRequiredService<SwitchableSecretStore>());
        services.AddSingleton<ISecretStorePromotion>(sp => sp.GetRequiredService<SwitchableSecretStore>());
        services.AddScoped<IFallbackSecretVault, FallbackSecretVault>();

        // Not registered as an IIntegrationConnector: Build() never actually calls out to
        // AnsibleOptions.Endpoint (it only assembles the extra_vars an AWX job launch sends —
        // AWX is the one real Ansible executor, see LaunchApplicationInstallationAwxJobHandler),
        // so a "reachability" card for it in System settings was misleading busywork. Removed at
        // the user's request (2026-09-16): "in sistem setting c'è ancora la configurazione di
        // ansible, sarebbe da togliere". AnsiblePlaybook/AnsibleInventory (the two fields Build()
        // does use) remain settable via SaveAnsibleIntegrationSettingsHandler directly.
        services.AddSingleton<AnsibleExecutionPackageBuilder>();
        services.AddSingleton<IAnsibleExecutionPackageBuilder>(sp => sp.GetRequiredService<AnsibleExecutionPackageBuilder>());

        // Scoped, not singleton: it depends on IIntegrationSettingsRepository (scoped, backed by
        // the per-request IrisDbContext) — its own constructor deps on the six options
        // singletons are fine the other way around (a scoped service may depend on singletons).
        services.TryAddScoped<IIntegrationSettingsReloader, IntegrationSettingsReloader>();

        // Same captive-dependency reasoning as the reloader above: it also reads
        // IIntegrationSettingsRepository (scoped), so it can't be a singleton either.
        services.TryAddScoped<IIntegrationConnectionTester, IntegrationConnectionTester>();

        services.AddSingleton<IIntegrationReachabilityProbe, IntegrationReachabilityProbe>();

        services.AddSingleton<AwxClient>();
        services.AddSingleton<IAwxClient>(sp => sp.GetRequiredService<AwxClient>());
        services.AddSingleton<IIntegrationConnector>(sp => sp.GetRequiredService<AwxClient>());

        services.AddSingleton<AzureDevOpsConnector>();
        services.AddSingleton<IIntegrationConnector>(sp => sp.GetRequiredService<AzureDevOpsConnector>());
        services.AddSingleton<IAzureDevOpsRepositoryReader>(sp => sp.GetRequiredService<AzureDevOpsConnector>());
        services.AddSingleton<IAzureDevOpsRepositoryWriter>(sp => sp.GetRequiredService<AzureDevOpsConnector>());

        services.AddSingleton<NexusConnector>();
        services.AddSingleton<IIntegrationConnector>(sp => sp.GetRequiredService<NexusConnector>());

        services.AddSingleton<IRemoteCommandRunner, SshCommandRunner>();

        services.AddSingleton<OpsHostConnector>();
        services.AddSingleton<IIntegrationConnector>(sp => sp.GetRequiredService<OpsHostConnector>());

        services.AddSingleton<AwxBlueprintDriftConnector>();
        services.AddSingleton<IIntegrationConnector>(sp => sp.GetRequiredService<AwxBlueprintDriftConnector>());
        services.AddSingleton<IAwxBlueprintReader>(sp => sp.GetRequiredService<AwxBlueprintDriftConnector>());
    }

    /// <summary>
    /// Best-effort, synchronous read of the (possibly not-yet-existing) IntegrationSettings
    /// row, using a throwaway DbContext built the same way IrisDbContextFactory builds one
    /// for design-time tooling — no DI container exists yet at this point in startup. Any
    /// failure (table not migrated yet, database unreachable) returns null; this must never
    /// throw and take the process down over what is purely a convenience read.
    /// </summary>
    private static IntegrationSettings? TryReadPersistedIntegrationSettings(
        DatabaseProvider provider,
        string connectionString,
        string migrationsAssembly)
    {
        try
        {
            var builder = new DbContextOptionsBuilder<IrisDbContext>();
            if (provider == DatabaseProvider.Postgres)
            {
                builder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(migrationsAssembly));
            }
            else
            {
                builder.UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(migrationsAssembly));
            }

            using var dbContext = new IrisDbContext(builder.Options);
            return dbContext.Set<IntegrationSettings>().AsNoTracking().SingleOrDefault();
        }
        catch (Exception ex)
        {
            // Deliberately broad: a fresh/unmigrated database, an unreachable Postgres server
            // at this exact point in a container-orchestrated startup, or any other transient
            // condition should all fall back to IConfiguration-only values identically — the
            // specific exception type doesn't change the recovery action.
            Console.Error.WriteLine(
                $"[Iris.Infrastructure] Could not read persisted integration settings at startup " +
                $"(falling back to configuration only): {ex.Message}");
            return null;
        }
    }

}
