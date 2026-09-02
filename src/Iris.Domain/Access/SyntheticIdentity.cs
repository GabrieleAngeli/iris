using System.Security.Cryptography;
using System.Text;

namespace Iris.Domain.Access;

/// <summary>
/// Shared derivation for the synthetic identity-provider object id used by every
/// non-federated way into Iris (dev-header trust, the local-password session, and the
/// setup-wizard super-admin). Deterministic from the email so
/// <c>UserProvisioningService.EnsureProvisionedAsync</c> — or a direct
/// <c>FindByExternalIdAsync</c> lookup — resolves the same real user every time,
/// whichever of these paths produced the claim.
/// </summary>
public static class SyntheticIdentity
{
    public static string DeriveObjectId(string email)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return new Guid(hash).ToString();
    }
}
