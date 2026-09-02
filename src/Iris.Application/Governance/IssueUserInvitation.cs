using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Governance;
using Iris.Domain.Access;

namespace Iris.Application.Governance;

/// <summary>
/// Command for <c>POST /governance/users/{userId}/invitation</c>: mints a fresh one-time
/// invitation token for a user and hands it to the configured notifier. Any invitation
/// already outstanding for that user is revoked — only the newest one is valid.
/// </summary>
public sealed record IssueUserInvitationCommand(Guid UserId);

public sealed class IssueUserInvitationHandler(
    IUserRepository users,
    IUserInvitationRepository invitations,
    IInvitationLinkBuilder linkBuilder,
    IInvitationNotifier notifier,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>How long a freshly issued invitation stays valid.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public async Task<InvitationResponse> HandleAsync(
        IssueUserInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.GetAsync(command.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("User", command.UserId);

        SelfGovernanceGuard.ThrowIfCurrentUser(user, currentUser);

        // Supersede any invitation still on file for this user.
        var existing = await invitations.GetForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
        foreach (var stale in existing)
        {
            invitations.Remove(stale);
        }

        var rawToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(rawToken);

        var issuedBy = currentUser.ExternalId is { Length: > 0 } externalId
            ? (await users.FindByExternalIdAsync(externalId, cancellationToken).ConfigureAwait(false))?.Id ?? Guid.Empty
            : Guid.Empty;

        var now = clock.UtcNow;
        var invitation = UserInvitation.Issue(Guid.CreateVersion7(), user.Id, tokenHash, issuedBy, now, Lifetime);

        await invitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var link = linkBuilder.BuildAcceptLink(rawToken);
        await notifier
            .SendAsync(new InvitationNotification(user.Email, user.DisplayName, link, invitation.ExpiresAtUtc), cancellationToken)
            .ConfigureAwait(false);

        return new InvitationResponse(user.Id, user.Email, user.DisplayName, rawToken, link, invitation.ExpiresAtUtc);
    }

    /// <summary>Hex-encoded SHA-256 — the only form of the token Iris keeps.</summary>
    public static string HashToken(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
