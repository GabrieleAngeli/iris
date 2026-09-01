using Iris.Domain.Access;
using Iris.Domain.Tenancy;

namespace Iris.Infrastructure.Persistence.Seeding;

internal static class SeedData
{
    public sealed record RoleSpec(
        Guid Id,
        string Key,
        string Name,
        string Description,
        IReadOnlyList<string> Permissions);

    public sealed record ContextSpec(Guid Id, string Name, ContextKind Kind);

    public sealed record CustomerSpec(Guid Id, string Key, string Name, IReadOnlyList<ContextSpec> Contexts);

    public sealed record UserSpec(
        Guid Id,
        string ExternalId,
        string Email,
        string DisplayName,
        Guid AssignmentId,
        Guid RoleId,
        AccessScope Scope);

    // ----- Role ids -----
    private static readonly Guid PlatformAdminRoleId = new("0a000000-0000-0000-0000-000000000001");
    private static readonly Guid CustomerAdminRoleId = new("0a000000-0000-0000-0000-000000000002");
    private static readonly Guid OperatorRoleId = new("0a000000-0000-0000-0000-000000000003");
    private static readonly Guid ReaderRoleId = new("0a000000-0000-0000-0000-000000000004");
    private static readonly Guid AuditorRoleId = new("0a000000-0000-0000-0000-000000000005");

    // ----- Customer / context ids -----
    private static readonly Guid ContosoId = new("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid ContosoTest = new("c0000000-0000-0000-0000-0000000000a1");
    private static readonly Guid ContosoStaging = new("c0000000-0000-0000-0000-0000000000a2");
    private static readonly Guid ContosoProduction = new("c0000000-0000-0000-0000-0000000000a3");
    private static readonly Guid GlobexId = new("c0000000-0000-0000-0000-000000000002");
    private static readonly Guid GlobexTest = new("c0000000-0000-0000-0000-0000000000b1");
    private static readonly Guid GlobexStaging = new("c0000000-0000-0000-0000-0000000000b2");
    private static readonly Guid GlobexProduction = new("c0000000-0000-0000-0000-0000000000b3");

    public static readonly IReadOnlyList<RoleSpec> BuiltInRoles =
    [
        new(
            PlatformAdminRoleId,
            "platform-admin",
            "Platform Administrator",
            "Full control over every customer, context and governance setting.",
            // Carries the platform.admin super-permission *and* every fine-grained code, so the
            // permission list reads as complete everywhere (roles view, /me assignments) — not
            // just as a single implied flag.
            Permissions.All),
        new(
            CustomerAdminRoleId,
            "customer-admin",
            "Customer Administrator",
            "Manages infrastructure, applications, deployments and governance for assigned customers.",
            [
                Permissions.Overview.Read,
                Permissions.Infrastructure.Read, Permissions.Infrastructure.Write, Permissions.Infrastructure.Delete,
                Permissions.Infrastructure.SecretsManage,
                Permissions.Applications.Read, Permissions.Applications.Write, Permissions.Applications.ImportKnowledge,
                Permissions.Deployments.Read, Permissions.Deployments.Write, Permissions.Deployments.Validate,
                Permissions.Deployments.Prepare,
                Permissions.Actions.Read, Permissions.Actions.Run,
                Permissions.Governance.Read, Permissions.Governance.ManageCustomers, Permissions.Governance.ManageRoles,
                Permissions.Governance.ManageAssignments, Permissions.Governance.ReadAudit,
            ]),
        new(
            OperatorRoleId,
            "operator",
            "Operator",
            "Prepares and runs deployments and actions for assigned scopes.",
            [
                Permissions.Overview.Read,
                Permissions.Infrastructure.Read,
                Permissions.Applications.Read, Permissions.Applications.ImportKnowledge,
                Permissions.Deployments.Read, Permissions.Deployments.Write, Permissions.Deployments.Validate,
                Permissions.Deployments.Prepare,
                Permissions.Actions.Read, Permissions.Actions.Run,
            ]),
        new(
            ReaderRoleId,
            "reader",
            "Reader",
            "Read-only visibility into assigned scopes.",
            [
                Permissions.Overview.Read,
                Permissions.Infrastructure.Read,
                Permissions.Applications.Read,
                Permissions.Deployments.Read,
                Permissions.Actions.Read,
                Permissions.Governance.Read,
            ]),
        new(
            AuditorRoleId,
            "auditor",
            "Auditor",
            "Read-only visibility plus access to audit history.",
            [
                Permissions.Overview.Read,
                Permissions.Infrastructure.Read,
                Permissions.Applications.Read,
                Permissions.Deployments.Read,
                Permissions.Actions.Read,
                Permissions.Governance.Read, Permissions.Governance.ReadAudit,
            ]),
    ];

    public static readonly IReadOnlyList<CustomerSpec> Customers =
    [
        new(ContosoId, "contoso", "Contoso Ltd.",
        [
            new(ContosoTest, "Test", ContextKind.Test),
            new(ContosoStaging, "Staging", ContextKind.Staging),
            new(ContosoProduction, "Production", ContextKind.Production),
        ]),
        new(GlobexId, "globex", "Globex Corporation",
        [
            new(GlobexTest, "Test", ContextKind.Test),
            new(GlobexStaging, "Staging", ContextKind.Staging),
            new(GlobexProduction, "Production", ContextKind.Production),
        ]),
    ];

    public static readonly IReadOnlyList<UserSpec> Users =
    [
        new(
            new Guid("d0000000-0000-0000-0000-000000000001"),
            "11111111-1111-1111-1111-111111111101",
            "admin@iris.local",
            "Iris Platform Admin",
            new Guid("e0000000-0000-0000-0000-000000000001"),
            PlatformAdminRoleId,
            AccessScope.Global()),
        new(
            new Guid("d0000000-0000-0000-0000-000000000002"),
            "11111111-1111-1111-1111-111111111102",
            "lucia@contoso.example",
            "Lucia Bianchi",
            new Guid("e0000000-0000-0000-0000-000000000002"),
            CustomerAdminRoleId,
            AccessScope.ForCustomer(ContosoId)),
        new(
            new Guid("d0000000-0000-0000-0000-000000000003"),
            "11111111-1111-1111-1111-111111111103",
            "marco@contoso.example",
            "Marco Rossi",
            new Guid("e0000000-0000-0000-0000-000000000003"),
            OperatorRoleId,
            AccessScope.ForContext(ContosoId, ContosoProduction)),
        new(
            new Guid("d0000000-0000-0000-0000-000000000004"),
            "11111111-1111-1111-1111-111111111104",
            "sara@iris.local",
            "Sara Verdi",
            new Guid("e0000000-0000-0000-0000-000000000004"),
            AuditorRoleId,
            AccessScope.Global()),
        new(
            new Guid("d0000000-0000-0000-0000-000000000005"),
            "11111111-1111-1111-1111-111111111105",
            "gio@globex.example",
            "Giovanni Neri",
            new Guid("e0000000-0000-0000-0000-000000000005"),
            ReaderRoleId,
            AccessScope.ForCustomer(GlobexId)),
    ];
}
