using Iris.Application.Abstractions;
using Iris.Infrastructure.Invitations;
using Iris.Infrastructure.Inventory;
using Iris.Infrastructure.Integrations;
using Iris.Infrastructure.Mail;
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
        services.AddScoped<IUserInvitationRepository, UserInvitationRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IEditLockRepository, EditLockRepository>();
        services.AddScoped<IMailProviderSettingsRepository, MailProviderSettingsRepository>();
        services.AddScoped<ITransactionLogRepository, TransactionLogRepository>();
        services.TryAddScoped<IServerInventoryProbe, MockServerInventoryProbe>();
        services.TryAddScoped<IDataServiceInventoryProbe, MockDataServiceInventoryProbe>();
        RegisterIntegrations(services, configuration);
        services.TryAddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.TryAddSingleton<IInvitationLinkBuilder, ConfiguredInvitationLinkBuilder>();
        services.TryAddScoped<IEmailSender, SmtpEmailSender>();
        services.TryAddScoped<IInvitationNotifier, SmtpInvitationNotifier>();
        services.AddScoped<IrisDbSeeder>();

        return services;
    }

    private static void RegisterIntegrations(IServiceCollection services, IConfiguration configuration)
    {
        var integrations = configuration.GetSection("Iris:Integrations");
        var openBao = new OpenBaoOptions
        {
            Endpoint = integrations["OpenBao:Endpoint"],
            Token = integrations["OpenBao:Token"],
            MountPath = integrations["OpenBao:MountPath"] ?? "secret",
            UseKvV2 = !bool.TryParse(integrations["OpenBao:UseKvV2"], out var useKvV2) || useKvV2
        };
        var ansible = new AnsibleOptions
        {
            Endpoint = integrations["Ansible:Endpoint"],
            Playbook = integrations["Ansible:Playbook"] ?? "iris-deploy-application.yml",
            Inventory = integrations["Ansible:Inventory"]
        };
        var awxEndpoint = integrations["AWX:Endpoint"];
        if (string.IsNullOrWhiteSpace(awxEndpoint))
        {
            awxEndpoint = integrations["Ansible:Endpoint"];
        }

        var awx = new AwxOptions
        {
            Endpoint = awxEndpoint,
            Token = integrations["AWX:Token"],
            JobTemplateId = int.TryParse(integrations["AWX:JobTemplateId"], out var jobTemplateId)
                ? jobTemplateId
                : null
        };

        services.AddSingleton(openBao);
        services.AddSingleton(ansible);
        services.AddSingleton(awx);

        services.AddSingleton<OpenBaoConnector>();
        services.AddSingleton<IIntegrationConnector>(sp => sp.GetRequiredService<OpenBaoConnector>());
        if (openBao.IsSecretStoreConfigured)
        {
            services.AddSingleton<ISecretStore, OpenBaoSecretStore>();
        }
        else
        {
            services.AddSingleton<ISecretStore, InMemorySecretStore>();
        }

        services.AddSingleton<AnsibleExecutionPackageBuilder>();
        services.AddSingleton<IAnsibleExecutionPackageBuilder>(sp => sp.GetRequiredService<AnsibleExecutionPackageBuilder>());
        services.AddSingleton<IIntegrationConnector>(sp => sp.GetRequiredService<AnsibleExecutionPackageBuilder>());

        services.AddSingleton<AwxClient>();
        services.AddSingleton<IAwxClient>(sp => sp.GetRequiredService<AwxClient>());
        services.AddSingleton<IIntegrationConnector>(sp => sp.GetRequiredService<AwxClient>());
    }
}
