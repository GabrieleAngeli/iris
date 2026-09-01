using Microsoft.AspNetCore.Authentication;

namespace Iris.Api.Auth;

public sealed class DevAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "Dev";

    /// <summary>Request header carrying the dev user's email.</summary>
    public string HeaderName { get; set; } = "X-Dev-User";

    /// <summary>
    /// Request header carrying the dev user's local password. Only required for users who have
    /// actually set one (<see cref="Iris.Domain.Access.User.HasPassword"/>); header-only sign-in
    /// keeps working for everyone else.
    /// </summary>
    public string PasswordHeaderName { get; set; } = "X-Dev-Password";

    /// <summary>Known dev identities. A request email must match one of these unless <see cref="AllowAnyEmail"/>.</summary>
    public IList<DevUser> Users { get; set; } = [];

    /// <summary>When true, an unknown email is accepted and a synthetic object id is derived from it.</summary>
    public bool AllowAnyEmail { get; set; }
}
