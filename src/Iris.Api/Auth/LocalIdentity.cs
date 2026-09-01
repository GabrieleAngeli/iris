using System.Security.Cryptography;
using System.Text;

namespace Iris.Api.Auth;

/// <summary>
/// Shared derivation for the synthetic identity-provider object id used by every non-federated
/// authentication path (dev-header trust, and the local-password session). Deterministic from the
/// email so <c>UserProvisioningService.EnsureProvisionedAsync</c> resolves the same real user on
/// every sign-in, whichever of these paths produced the claim.
/// </summary>
internal static class LocalIdentity
{
    public static string DeriveObjectId(string email)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return new Guid(hash).ToString();
    }
}
