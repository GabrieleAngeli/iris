using System.Collections.Immutable;

namespace Iris.Domain.Access;

/// <summary>
/// Canonical catalog of every fine-grained permission Iris recognises. Grouped to
/// mirror the operator navigation (Overview, Infrastructure, Applications,
/// Deployments, Actions, Governance).
/// </summary>
public static class Permissions
{
    public static class Overview
    {
        public const string Read = "overview.read";
    }

    public static class Infrastructure
    {
        public const string Read = "infrastructure.read";
        public const string Write = "infrastructure.write";
        public const string Delete = "infrastructure.delete";

        /// <summary>Rotate/replace the secret behind a server credential (lead/manager only).</summary>
        public const string SecretsManage = "infrastructure.secrets.manage";
    }

    public static class Applications
    {
        public const string Read = "applications.read";
        public const string Write = "applications.write";
        public const string ImportKnowledge = "applications.import";
    }

    public static class Deployments
    {
        public const string Read = "deployments.read";
        public const string Write = "deployments.write";
        public const string Validate = "deployments.validate";
        public const string Prepare = "deployments.prepare";
    }

    public static class Actions
    {
        public const string Read = "actions.read";
        public const string Run = "actions.run";
    }

    public static class Governance
    {
        public const string Read = "governance.read";
        public const string ManageCustomers = "governance.customers.manage";
        public const string ManageRoles = "governance.roles.manage";
        public const string ManageAssignments = "governance.assignments.manage";
        public const string ReadAudit = "governance.audit.read";
    }

    /// <summary>Platform super-user. Implies every other permission at every scope.</summary>
    public const string PlatformAdmin = "platform.admin";

    /// <summary>Every permission code known to the platform, including <see cref="PlatformAdmin"/>.</summary>
    public static readonly ImmutableArray<string> All =
    [
        Overview.Read,
        Infrastructure.Read, Infrastructure.Write, Infrastructure.Delete, Infrastructure.SecretsManage,
        Applications.Read, Applications.Write, Applications.ImportKnowledge,
        Deployments.Read, Deployments.Write, Deployments.Validate, Deployments.Prepare,
        Actions.Read, Actions.Run,
        Governance.Read, Governance.ManageCustomers, Governance.ManageRoles,
        Governance.ManageAssignments, Governance.ReadAudit,
        PlatformAdmin,
    ];

    private static readonly ImmutableHashSet<string> Known = All.ToImmutableHashSet(StringComparer.Ordinal);

    public static bool IsKnown(string permissionCode) => Known.Contains(permissionCode);
}
