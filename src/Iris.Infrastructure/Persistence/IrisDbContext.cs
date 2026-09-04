using Iris.Domain.Access;
using Iris.Domain.Applications;
using Iris.Domain.Audit;
using Iris.Domain.Infrastructure;
using Iris.Domain.Settings;
using Iris.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence;

public sealed class IrisDbContext(DbContextOptions<IrisDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<EditLock> EditLocks => Set<EditLock>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerContext> CustomerContexts => Set<CustomerContext>();

    public DbSet<ServerNode> Servers => Set<ServerNode>();

    public DbSet<DataServiceInstance> DataServices => Set<DataServiceInstance>();

    public DbSet<ServerCredential> ServerCredentials => Set<ServerCredential>();

    public DbSet<ApplicationDefinition> Applications => Set<ApplicationDefinition>();

    public DbSet<ApplicationVersion> ApplicationVersions => Set<ApplicationVersion>();

    public DbSet<ConfigurationKey> ApplicationConfigurationKeys => Set<ConfigurationKey>();

    public DbSet<DependencyDefinition> ApplicationDependencies => Set<DependencyDefinition>();

    public DbSet<PlaceholderDefinition> ApplicationPlaceholders => Set<PlaceholderDefinition>();

    public DbSet<ApplicationUnitDefinition> ApplicationUnits => Set<ApplicationUnitDefinition>();

    public DbSet<InstallationProfileDefinition> InstallationProfiles => Set<InstallationProfileDefinition>();

    public DbSet<DependencyConstraintDefinition> ApplicationDependencyConstraints => Set<DependencyConstraintDefinition>();

    public DbSet<ApplicationInstallation> ApplicationInstallations => Set<ApplicationInstallation>();

    public DbSet<ApplicationInstallationBinding> ApplicationInstallationBindings => Set<ApplicationInstallationBinding>();

    public DbSet<MailProviderSettings> MailProviderSettings => Set<MailProviderSettings>();

    public DbSet<TransactionLogEntry> TransactionLogEntries => Set<TransactionLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IrisDbContext).Assembly);
    }
}
