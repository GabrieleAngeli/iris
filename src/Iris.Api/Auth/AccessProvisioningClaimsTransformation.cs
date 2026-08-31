using System.Security.Claims;
using Iris.Application.Abstractions;
using Iris.Application.Access;
using Microsoft.AspNetCore.Authentication;

namespace Iris.Api.Auth;

/// <summary>
/// Runs on every authenticated request: ensures the principal has a matching
/// <see cref="Iris.Domain.Access.User"/> row (just-in-time provisioning) and stamps
/// the internal user id as an <c>iris:uid</c> claim so it runs at most once per request.
/// </summary>
public sealed class AccessProvisioningClaimsTransformation(
    ICurrentUser currentUser,
    IUserProvisioningService provisioning) : IClaimsTransformation
{
    public const string InternalUserIdClaim = "iris:uid";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        if (principal.HasClaim(c => c.Type == InternalUserIdClaim))
        {
            return principal;
        }

        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.ExternalId))
        {
            return principal;
        }

        var user = await provisioning.EnsureProvisionedAsync(currentUser).ConfigureAwait(false);

        if (principal.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(InternalUserIdClaim, user.Id.ToString()));
        }

        return principal;
    }
}
