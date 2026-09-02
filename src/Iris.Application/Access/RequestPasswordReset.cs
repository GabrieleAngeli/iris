using System.Buffers.Text;
using System.Security.Cryptography;
using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Application.Governance;
using Iris.Contracts.Access;
using Iris.Domain.Access;

namespace Iris.Application.Access;

public sealed record RequestPasswordResetCommand(string Email);

public sealed class RequestPasswordResetHandler(
    IUserRepository users,
    IUserInvitationRepository invitations,
    IInvitationLinkBuilder linkBuilder,
    IInvitationNotifier notifier,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<RequestPasswordResetResponse> HandleAsync(
        RequestPasswordResetCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var email = command.Email?.Trim() ?? string.Empty;
        if (email.Length == 0)
        {
            throw new ValidationException("Email is required.");
        }

        var user = await users.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            return new RequestPasswordResetResponse(Sent: true);
        }

        var existing = await invitations.GetForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
        foreach (var stale in existing)
        {
            invitations.Remove(stale);
        }

        var rawToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = IssueUserInvitationHandler.HashToken(rawToken);
        var invitation = UserInvitation.Issue(
            Guid.CreateVersion7(),
            user.Id,
            tokenHash,
            issuedByUserId: Guid.Empty,
            clock.UtcNow,
            IssueUserInvitationHandler.Lifetime);

        await invitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var link = linkBuilder.BuildAcceptLink(rawToken);
        await notifier
            .SendAsync(new InvitationNotification(user.Email, user.DisplayName, link, invitation.ExpiresAtUtc), cancellationToken)
            .ConfigureAwait(false);

        return new RequestPasswordResetResponse(Sent: true);
    }
}
