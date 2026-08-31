using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Iris.Api.Auth;

/// <summary>
/// Local-development authentication: the caller supplies an email in a request
/// header and is signed in as the matching configured <see cref="DevUser"/>.
/// Never registered when the auth mode is EntraId-only.
/// </summary>
public sealed class DevAuthenticationHandler(
    IOptionsMonitor<DevAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<DevAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var email = headerValues.ToString().Trim();
        if (string.IsNullOrEmpty(email))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Empty '{Options.HeaderName}' header."));
        }

        var user = Options.Users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

        string objectId;
        string displayName;
        if (user is not null)
        {
            objectId = string.IsNullOrWhiteSpace(user.ObjectId) ? DeriveObjectId(email) : user.ObjectId;
            displayName = string.IsNullOrWhiteSpace(user.Name) ? email : user.Name;
        }
        else if (Options.AllowAnyEmail)
        {
            objectId = DeriveObjectId(email);
            displayName = email;
        }
        else
        {
            return Task.FromResult(AuthenticateResult.Fail($"Unknown dev user '{email}'."));
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
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static string DeriveObjectId(string email)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return new Guid(hash).ToString();
    }
}
