using Iris.Domain.Access;
using Iris.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Iris.Infrastructure.Persistence;

public sealed class IrisDbContext(DbContextOptions<IrisDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerContext> CustomerContexts => Set<CustomerContext>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IrisDbContext).Assembly);
    }
}
