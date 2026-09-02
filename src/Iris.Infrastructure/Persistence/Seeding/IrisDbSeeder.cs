using Iris.Domain.Access;
using Iris.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Iris.Infrastructure.Persistence.Seeding;

/// <summary>
/// Populates the built-in roles and a demo tenancy (customers, contexts, users and
/// their assignments). Safe to run repeatedly: each section is skipped when data
/// already exists.
/// </summary>
public sealed class IrisDbSeeder(IrisDbContext dbContext, ILogger<IrisDbSeeder> logger)
{
    /// <summary>
    /// The built-in role catalog is seeded unconditionally — it's the permission system, not
    /// demo data. <paramref name="seedDemoData"/> gates the reference tenancy (Contoso/Globex,
    /// the 5 demo users including a ready-made <c>admin@iris.local</c>): on in Development
    /// (<c>Iris:Database:SeedDemoData</c>), off elsewhere, so a real deployment starts genuinely
    /// empty and the first-run setup wizard is what creates its first super-admin.
    /// </summary>
    public async Task SeedAsync(bool seedDemoData, CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken).ConfigureAwait(false);

        if (seedDemoData)
        {
            await SeedCustomersAsync(cancellationToken).ConfigureAwait(false);
            await SeedUsersAndAssignmentsAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        // Built-in roles are authoritative: create any that are missing and reconcile the
        // name / description / permission set of the rest on every startup, so changes to
        // SeedData (e.g. platform-admin gaining every permission) reach an existing database
        // without a wipe. Operator-created roles are left untouched.
        var existing = await dbContext.Roles
            .Where(r => r.IsBuiltIn)
            .ToDictionaryAsync(r => r.Id, cancellationToken)
            .ConfigureAwait(false);

        var created = 0;
        var reconciled = 0;

        foreach (var (id, key, name, description, permissions) in SeedData.BuiltInRoles)
        {
            var permissionIds = permissions.Select(PermissionId.Parse).ToArray();

            if (existing.TryGetValue(id, out var role))
            {
                if (!role.Permissions.OrderBy(p => p, StringComparer.Ordinal)
                        .SequenceEqual(permissionIds.Select(p => p.Value).OrderBy(p => p, StringComparer.Ordinal)))
                {
                    role.ReplacePermissions(permissionIds);
                    reconciled++;
                }

                continue;
            }

            var fresh = new Role(id, key, name, description, isBuiltIn: true);
            fresh.ReplacePermissions(permissionIds);
            dbContext.Roles.Add(fresh);
            created++;
        }

        if (created + reconciled > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Built-in roles: {Created} created, {Reconciled} reconciled.", created, reconciled);
        }
    }

    private async Task SeedCustomersAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Customers.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        foreach (var spec in SeedData.Customers)
        {
            var customer = new Customer(spec.Id, spec.Key, spec.Name);
            foreach (var (contextId, contextName, kind) in spec.Contexts)
            {
                customer.AddContext(contextId, contextName, kind);
            }

            dbContext.Customers.Add(customer);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Seeded {Count} demo customers.", SeedData.Customers.Count);
    }

    private async Task SeedUsersAndAssignmentsAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        foreach (var spec in SeedData.Users)
        {
            dbContext.Users.Add(new User(spec.Id, spec.ExternalId, spec.Email, spec.DisplayName));
            dbContext.RoleAssignments.Add(new RoleAssignment(spec.AssignmentId, spec.Id, spec.RoleId, spec.Scope));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Seeded {Count} demo users with assignments.", SeedData.Users.Count);
    }
}
