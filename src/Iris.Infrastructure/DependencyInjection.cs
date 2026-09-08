using Iris.Application.Abstractions;
using Iris.Domain.Settings;
using Iris.Infrastructure.Containers;
using Iris.Infrastructure.Invitations;
using Iris.Infrastructure.Inventory;
using Iris.Infrastructure.Integrations;
using Iris.Infrastructure.Mail;
using Iris.Infrastructure.Persistence;
using Iris.Infrastructure.Processes;
using Iris.Infrastructure.Persistence.Interceptors;
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
        services.AddScoped<IEnvironmentServerAssignmentRepository, EnvironmentServerAssignmentRepository>();
        services.AddScoped<IUserInvitationRepository, UserInvitationRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IEditLockRepository, EditLockRepository>();
        services.AddScoped<IMailProviderSettingsRepository, MailProviderSettingsRepository>();
        services.AddScoped<IIntegrationSettingsRepository, IntegrationSettingsRepository>();
        services.AddScoped<IFallbackSecretEntryRepository, FallbackSecretEntryRepository>();
        services.AddScoped<ITransactionLogRepository, TransactionLogRepository>();
        services.TryAddScoped<IServerInventoryProbe, MockServerInventoryProbe>();
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
        // OpenBaoOptions/AwxOptions/AnsibleOptions properties are init-only, so every value
        // has to be resolved (config vs. persisted) *before* constructing them, not after.
        var persisted = TryReadPersistedIntegrationSettings(provider, connectionString, migrationsAssembly);

        var openBaoEndpoint = !string.IsNullOrWhiteSpace(persisted?.OpenBaoEndpoint)
            ? persisted.OpenBaoEndpoint
            : integrations["OpenBao:Endpoint"];
        var openBaoMountPath = !string.IsNullOrWhiteSpace(persisted?.OpenBaoEndpoint)
            ? persisted!.OpenBaoMountPath
            : integrations["OpenBao:MountPath"] ?? "secret";
        var openBaoUseKvV2 = !string.IsNullOrWhiteSpace(persisted?.OpenBaoEndpoint)
            ? persisted!.OpenBaoUseKvV2
            : !bool.TryParse(integrations["OpenBao:UseKvV2"], out var useKvV2) || useKvV2;
        // OpenBao's own token is never resolved from a persisted reference here — doing so
        // would require a working OpenBao connection authenticated with that very token
        // (circular). Only a token given directly via IConfiguration bootstraps OpenBao
        // itself; a token saved through the wizard/UI needs re-entering once after the
        // restart that's supposed to activate real OpenBao (see the plan's documented
        // limitation) — this is a deliberate simplification, not an oversight.
        var openBaoToken = integrations["OpenBao:Token"];

        var openBao = new OpenBaoOptions
        {
            Endpoint = openBaoEndpoint,
            Token = openBaoToken,
            MountPath = openBaoMountPath,
            UseKvV2 = openBaoUseKvV2
        };

        var ansibleEndpoint = !string.IsNullOrWhiteSpace(persisted?.AnsibleEndpoint)
            ? persisted.AnsibleEndpoint
            : integrations["Ansible:Endpoint"];
        var ansiblePlaybook = !string.IsNullOrWhiteSpace(persisted?.AnsibleEndpoint)
            ? persisted!.AnsiblePlaybook
            : integrations["Ansible:Playbook"] ?? "iris-deploy-application.yml";
        var ansibleInventory = !string.IsNullOrWhiteSpace(persisted?.AnsibleEndpoint)
            ? persisted!.AnsibleInventory
            : integrations["Ansible:Inventory"];

        var ansible = new AnsibleOptions
        {
            Endpoint = ansibleEndpoint,
            Playbook = ansiblePlaybook,
            Inventory = ansibleInventory
        };

        var awxEndpoint = !string.IsNullOrWhiteSpace(persisted?.AwxEndpoint)
            ? persisted.AwxEndpoint
            : integrations["AWX:Endpoint"];
        if (string.IsNullOrWhiteSpace(awxEndpoint))
        {
            awxEndpoint = integrations["Ansible:Endpoint"];
        }

        var awxJobTemplateId = !string.IsNullOrWhiteSpace(persisted?.AwxEndpoint)
            ? persisted!.AwxJobTemplateId
            : int.TryParse(integrations["AWX:JobTemplateId"], out var jobTemplateId) ? jobTemplateId : null;

        // Unlike OpenBao's own token, AWX's token CAN be resolved here — it's stored behind
        // a (by this point, hopefully) fully-configured OpenBao, not behind itself.
        var awxToken = !string.IsNullOrWhiteSpace(persisted?.AwxTokenSecretReference)
            ? ResolvePersistedToken(persisted!.AwxTokenSecretReference, openBao)
            : integrations["AWX:Token"];

        var awx = new AwxOptions
        {
            Endpoint = awxEndpoint,
            Token = awxToken,
            JobTemplateId = awxJobTemplateId
        };

        // What this process actually locked in, captured once — compared against a fresh DB
        // read on every GET /system/settings to tell the operator whether a since-saved
        // change still needs a restart (see GetSystemSettingsHandler).
        services.AddSingleton(new ActiveIntegrationSnapshot(openBao.Endpoint, awx.Endpoint, ansible.Endpoint));

        services.AddSingleton(openBao);
        services.AddSingleton(ansible);
        services.AddSingleton(awx);

        services.AddSingleton<OpenBaoConnector>();
        services.AddSingleton<IIntegrationConnector>(sp => sp.GetRequiredService<OpenBaoConnector>());
        if (openBao.IsSecretStoreConfigured)
        {
            services.AddSingleton<ISecretStore, OpenBaoSecretStore>();
            // Nothing to unlock once OpenBao itself is the active store — every secret already
            // goes through real OpenBao, encrypted at rest by OpenBao itself.
            services.AddScoped<IFallbackSecretVault, NullFallbackSecretVault>();
        }
        else
        {
            // Same in-memory-dictionary behavior as the InMemorySecretStore it replaces for every
            // ISecretStore caller — the encrypted-at-rest DB persistence is a separate,
            // password-gated capability layered on top, never touching this contract. See
            // EncryptedFallbackSecretStore/FallbackSecretVault remarks for the full design.
            services.AddSingleton<EncryptedFallbackSecretStore>();
            services.AddSingleton<ISecretStore>(sp => sp.GetRequiredService<EncryptedFallbackSecretStore>());
            services.AddSingleton<IFallbackSecretCache>(sp => sp.GetRequiredService<EncryptedFallbackSecretStore>());
            services.AddScoped<IFallbackSecretVault, FallbackSecretVault>();
        }

        services.AddSingleton<AnsibleExecutionPackageBuilder>();
        services.AddSingleton<IAnsibleExecutionPackageBuilder>(sp => sp.GetRequiredService<AnsibleExecutionPackageBuilder>());
        services.AddSingleton<IIntegrationConnector>(sp => sp.GetRequiredService<AnsibleExecutionPackageBuilder>());

        services.AddSingleton<AwxClient>();
        services.AddSingleton<IAwxClient>(sp => sp.GetRequiredService<AwxClient>());
        services.AddSingleton<IIntegrationConnector>(sp => sp.GetRequiredService<AwxClient>());
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

    /// <summary>
    /// Resolves a secret reference saved by a previous PUT /system/integrations/* call back
    /// into its raw value, using <paramref name="resolverOpenBao"/> as the (possibly not yet
    /// usable) OpenBao connection to resolve it through. Returns null — logging why, never
    /// throwing — if the reference can't be resolved: a <c>mock-openbao:</c> reference never
    /// survives a restart (it was only ever in that prior process's memory), and an
    /// <c>openbao://</c> reference can't be resolved until <paramref name="resolverOpenBao"/>
    /// itself already has a working endpoint+token.
    /// </summary>
    private static string? ResolvePersistedToken(string? reference, OpenBaoOptions resolverOpenBao)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        if (!resolverOpenBao.IsSecretStoreConfigured)
        {
            Console.Error.WriteLine(
                $"[Iris.Infrastructure] Cannot resolve secret reference '{reference}' at startup: " +
                "the OpenBao connection needed to resolve it isn't itself configured yet.");
            return null;
        }

        try
        {
            using var resolver = new OpenBaoSecretStore(resolverOpenBao);
            return resolver.RetrieveAsync(reference).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[Iris.Infrastructure] Could not resolve secret reference '{reference}' at startup: {ex.Message}");
            return null;
        }
    }
}
