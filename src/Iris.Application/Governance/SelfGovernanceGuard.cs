using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Domain.Access;

namespace Iris.Application.Governance;

internal static class SelfGovernanceGuard
{
    private const string Message = "You cannot manage your own Iris user account from Governance.";

    public static void ThrowIfCurrentUser(User target, ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated)
        {
            return;
        }

        if (SameExternalId(target, currentUser) || SameEmail(target, currentUser))
        {
            throw new ForbiddenException(Message);
        }
    }

    public static async Task ThrowIfCurrentUserAsync(
        Guid targetUserId,
        IUserRepository users,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return;
        }

        var externalId = currentUser.ExternalId?.Trim();
        var caller = !string.IsNullOrWhiteSpace(externalId)
            ? await users.FindByExternalIdAsync(externalId, cancellationToken).ConfigureAwait(false)
            : null;

        var email = currentUser.Email?.Trim();
        caller ??= !string.IsNullOrWhiteSpace(email)
            ? await users.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false)
            : null;

        if (caller?.Id == targetUserId)
        {
            throw new ForbiddenException(Message);
        }
    }

    private static bool SameExternalId(User target, ICurrentUser currentUser) =>
        !string.IsNullOrWhiteSpace(currentUser.ExternalId)
        && string.Equals(target.ExternalId, currentUser.ExternalId.Trim(), StringComparison.Ordinal);

    private static bool SameEmail(User target, ICurrentUser currentUser) =>
        !string.IsNullOrWhiteSpace(currentUser.Email)
        && string.Equals(target.Email, currentUser.Email.Trim(), StringComparison.OrdinalIgnoreCase);
}
