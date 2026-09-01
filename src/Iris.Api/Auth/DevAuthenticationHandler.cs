using System.Security.Claims;
using System.Text.Encodings.Web;
using Iris.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Iris.Api.Auth;

/// <summary>
/// Local-development authentication: the caller supplies an email in a request
/// header and is signed in as the matching configured <see cref="DevUser"/>. If that
/// user has set a local password (non-SSO), a matching password header is also
/// required. Never registered when the auth mode is EntraId-only.
/// </summary>
public sealed class DevAuthenticationHandler(
    IOptionsMonitor<DevAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<DevAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var email = headerValues.ToString().Trim();
        if (string.IsNullOrEmpty(email))
        {
            return AuthenticateResult.Fail($"Empty '{Options.HeaderName}' header.");
        }

        var user = Options.Users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

        string objectId;
        string displayName;
        if (user is not null)
        {
            objectId = string.IsNullOrWhiteSpace(user.ObjectId) ? LocalIdentity.DeriveObjectId(email) : user.ObjectId;
            displayName = string.IsNullOrWhiteSpace(user.Name) ? email : user.Name;
        }
        else if (Options.AllowAnyEmail)
        {
            objectId = LocalIdentity.DeriveObjectId(email);
            displayName = email;
        }
        else
        {
            return AuthenticateResult.Fail($"Unknown dev user '{email}'.");
        }

        var passwordCheck = await CheckLocalPasswordAsync(email).ConfigureAwait(false);
        if (passwordCheck is { } failure)
        {
            return failure;
        }

        Claim[] claims =
        [
            new("oid", objectId),
            new(ClaimTypes.NameIdentifier, objectId),
            new("preferred_username", email),
            new(ClaimTypes.Email, email),
            new("name", displayName),
            new(ClaimTypes.Name, displayName),
        ];

        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, roleType: null);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    /// <summary>
    /// Returns a failure result when the user has a local password and the request's password
    /// header is missing or wrong; <c>null</c> when the request may proceed (no password set yet,
    /// or the supplied one matches).
    /// </summary>
    private async Task<AuthenticateResult?> CheckLocalPasswordAsync(string email)
    {
        var services = Context.RequestServices;
        var users = services.GetRequiredService<IUserRepository>();

        var account = await users.FindByEmailAsync(email).ConfigureAwait(false);
        if (account is not { PasswordHash: { } hash })
        {
            return null;
        }

        var supplied = Request.Headers.TryGetValue(Options.PasswordHeaderName, out var values)
            ? values.ToString()
            : string.Empty;

        if (string.IsNullOrEmpty(supplied))
        {
            return AuthenticateResult.Fail(
                $"'{email}' has a local password; supply it in the '{Options.PasswordHeaderName}' header.");
        }

        var hasher = services.GetRequiredService<IPasswordHasher>();
        return hasher.Verify(supplied, hash)
            ? null
            : AuthenticateResult.Fail("Incorrect password.");
    }

}
