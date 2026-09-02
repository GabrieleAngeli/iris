using System.Security.Claims;
using Iris.Application.Abstractions;

namespace Iris.Api.Auth;

/// <summary>Projects <see cref="ICurrentUser"/> from the authenticated principal on the current request.</summary>
public sealed class ClaimsPrincipalCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private static readonly string[] ExternalIdClaims =
    [
        "oid",
        "http://schemas.microsoft.com/identity/claims/objectidentifier",
        ClaimTypes.NameIdentifier,
        "sub",
    ];

    private static readonly string[] EmailClaims =
    [
        "preferred_username",
        "email",
        ClaimTypes.Email,
        "upn",
    ];

    private static readonly string[] NameClaims =
    [
        "name",
        ClaimTypes.Name,
    ];

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(AccessProvisioningClaimsTransformation.InternalUserIdClaim), out var id)
            ? id
            : null;

    public string? ExternalId => FirstClaim(ExternalIdClaims);

    public string? Email => FirstClaim(EmailClaims);

    public string? DisplayName => FirstClaim(NameClaims);

    private string? FirstClaim(string[] types)
    {
        var principal = Principal;
        if (principal is null)
        {
            return null;
        }

        foreach (var type in types)
        {
            var value = principal.FindFirstValue(type);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
