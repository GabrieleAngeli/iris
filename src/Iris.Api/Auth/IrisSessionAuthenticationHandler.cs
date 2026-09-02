using System.Security.Claims;
using System.Text.Encodings.Web;
using Iris.Application.Abstractions;
using Iris.Application.Governance;
using Iris.Domain.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Iris.Api.Auth;

/// <summary>
/// Validates the session token <c>POST /auth/login</c> issues — the local-password counterpart to
/// an SSO-issued bearer token, distinguished from one by shape (a JWT always has two dots; this
/// opaque token never does — see <c>AuthenticationSetup</c>'s scheme selector). Returns
/// <see cref="AuthenticateResult.NoResult"/> rather than <see cref="AuthenticateResult.Fail"/> for
/// anything it can't validate, since a token routed here is already known not to be a JWT.
/// </summary>
public sealed class IrisSessionAuthenticationHandler(
    IOptionsMonitor<IrisSessionAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<IrisSessionAuthenticationOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header[BearerPrefix.Length..].Trim();
        if (token.Length == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var services = Context.RequestServices;
        var sessions = services.GetRequiredService<IUserSessionRepository>();
        var clock = services.GetRequiredService<IClock>();

        var tokenHash = IssueUserInvitationHandler.HashToken(token);
        var session = await sessions.FindByTokenHashAsync(tokenHash).ConfigureAwait(false);
        if (session is null || !session.IsValid(clock.UtcNow))
        {
            return AuthenticateResult.NoResult();
        }

        var users = services.GetRequiredService<IUserRepository>();
        var user = await users.GetAsync(session.UserId).ConfigureAwait(false);
        if (user is null)
        {
            return AuthenticateResult.NoResult();
        }

        var objectId = SyntheticIdentity.DeriveObjectId(user.Email);

        Claim[] claims =
        [
            new("oid", objectId),
            new(ClaimTypes.NameIdentifier, objectId),
            new("preferred_username", user.Email),
            new(ClaimTypes.Email, user.Email),
            new("name", user.DisplayName),
            new(ClaimTypes.Name, user.DisplayName),
        ];

        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, roleType: null);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
