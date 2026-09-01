using Microsoft.AspNetCore.Authentication;

namespace Iris.Api.Auth;

/// <summary>
/// Options for the local-password session scheme (<see cref="IrisSessionAuthenticationHandler"/>).
/// No configurable knobs today — kept as a distinct options type so the scheme is independently
/// addressable, matching every other <see cref="AuthenticationSchemeOptions"/> in this project.
/// </summary>
public sealed class IrisSessionAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "IrisSession";
}
