using Iris.Domain.Access;
using Iris.Domain.Infrastructure;
using Iris.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence;

public sealed class IrisDbContext(DbContextOptions<IrisDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    public DbSet<EditLock> EditLocks => Set<EditLock>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerContext> CustomerContexts => Set<CustomerContext>();

    public DbSet<ServerNode> Servers => Set<ServerNode>();

    public DbSet<ServerCredential> ServerCredentials => Set<ServerCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IrisDbContext).Assembly);
    }
}
