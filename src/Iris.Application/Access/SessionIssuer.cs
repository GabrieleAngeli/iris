using System.Buffers.Text;
using System.Security.Cryptography;
using Iris.Application.Abstractions;
using Iris.Application.Governance;
using Iris.Domain.Access;

namespace Iris.Application.Access;

/// <summary>
/// Mints a <see cref="UserSession"/> for a user already known to be who they say they are —
/// shared by <see cref="LoginHandler"/> and the setup wizard's <c>CompleteSetupHandler</c> so
/// the token-generation/hashing logic exists in exactly one place. Adds the session to the
/// tracked context; the caller still owns calling <c>IUnitOfWork.SaveChangesAsync</c>.
/// </summary>
public sealed class SessionIssuer(IUserSessionRepository sessions, IClock clock)
{
    /// <summary>How long a freshly issued session stays valid before sign-in is required again.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);

    public async Task<(string RawToken, DateTimeOffset ExpiresAtUtc)> IssueAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rawToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = IssueUserInvitationHandler.HashToken(rawToken);

        var now = clock.UtcNow;
        var session = UserSession.Issue(Guid.CreateVersion7(), userId, tokenHash, now, SessionLifetime);

        await sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);

        return (rawToken, session.ExpiresAtUtc);
    }
}
