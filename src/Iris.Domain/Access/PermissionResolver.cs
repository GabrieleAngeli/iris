namespace Iris.Domain.Access;

/// <summary>
/// Pure evaluation of effective permissions: given the grants a user holds and a
/// target scope, produce the union of permissions whose grant scope covers the
/// target. <see cref="Permissions.PlatformAdmin"/> short-circuits to "everything".
/// </summary>
public static class PermissionResolver
{
    /// <summary>Effective permission codes for <paramref name="target"/>.</summary>
    public static IReadOnlySet<string> Resolve(IEnumerable<EffectiveGrant> grants, AccessScope target)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(target);

        var effective = new HashSet<string>(StringComparer.Ordinal);

        foreach (var grant in grants)
        {
            if (!grant.Scope.Covers(target))
            {
                continue;
            }

            foreach (var permission in grant.Permissions)
            {
                effective.Add(permission);
            }
        }

        if (effective.Contains(Permissions.PlatformAdmin))
        {
            return Permissions.All.ToHashSet(StringComparer.Ordinal);
        }

        return effective;
    }

    /// <summary>True when the grants allow <paramref name="permission"/> at <paramref name="target"/>.</summary>
    public static bool IsAllowed(IEnumerable<EffectiveGrant> grants, PermissionId permission, AccessScope target)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(target);

        foreach (var grant in grants)
        {
            if (!grant.Scope.Covers(target))
            {
                continue;
            }

            if (grant.Permissions.Contains(Permissions.PlatformAdmin) ||
                grant.Permissions.Contains(permission.Value))
            {
                return true;
            }
        }

        return false;
    }
}
