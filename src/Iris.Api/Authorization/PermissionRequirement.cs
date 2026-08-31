using Microsoft.AspNetCore.Authorization;

namespace Iris.Api.Authorization;

/// <summary>Requires the caller to hold a specific fine-grained permission at the request's scope.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Builds and parses the policy names that carry a <see cref="PermissionRequirement"/>.</summary>
public static class PermissionPolicy
{
    public const string Prefix = "perm:";

    public static string Name(string permission) => Prefix + permission;

    public static bool TryGetPermission(string policyName, out string permission)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            permission = policyName[Prefix.Length..];
            return permission.Length > 0;
        }

        permission = string.Empty;
        return false;
    }
}
