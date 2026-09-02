using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Application.Common;
using Iris.Contracts.Governance;

namespace Iris.Application.Governance;

/// <summary>
/// Command for <c>POST /invitations/accept</c> — the recipient side of <see cref="IssueUserInvitationCommand"/>.
/// Sets the invited user's first local password and consumes the one-time token; it does not sign
/// the caller in — they go on to <c>POST /auth/login</c> with the password they just set, which
/// reconciles their identity exactly like a first dev-mode sign-in already does today.
/// </summary>
public sealed record AcceptInvitationCommand(string Token, string NewPassword);

public sealed class AcceptInvitationHandler(
    IUserRepository users,
    IUserInvitationRepository invitations,
    IPasswordHasher passwordHasher,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<AcceptInvitationResponse> HandleAsync(
        AcceptInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Token))
        {
            throw new ValidationException("This invitation is invalid or has expired.");
        }

        var newPassword = command.NewPassword ?? string.Empty;
        if (newPassword.Length < SetMyPasswordHandler.MinimumLength)
        {
            throw new ValidationException($"The new password must be at least {SetMyPasswordHandler.MinimumLength} characters.");
        }

        var tokenHash = IssueUserInvitationHandler.HashToken(command.Token.Trim());
        var invitation = await invitations.FindByTokenHashAsync(tokenHash, cancellationToken).ConfigureAwait(false);

        var now = clock.UtcNow;
        if (invitation is null || !invitation.IsPending(now))
        {
            throw new ValidationException("This invitation is invalid or has expired.");
        }

        var user = await users.GetAsync(invitation.UserId, cancellationToken).ConfigureAwait(false)
            ?? throw new ValidationException("This invitation is invalid or has expired.");

        user.SetPassword(passwordHasher.Hash(newPassword), now);
        invitation.Consume(now);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AcceptInvitationResponse(user.Email);
    }
}
