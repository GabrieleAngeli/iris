using Iris.Application.Abstractions;
using Iris.Domain.Access;

namespace Iris.Application.Access;

internal sealed class UserProvisioningService(IUserRepository users, IUnitOfWork unitOfWork) : IUserProvisioningService
{
    public async Task<User> EnsureProvisionedAsync(
        ICurrentUser principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (!principal.IsAuthenticated || string.IsNullOrWhiteSpace(principal.ExternalId))
        {
            throw new InvalidOperationException("Cannot provision a user from an unauthenticated principal.");
        }

        var email = Coalesce(principal.Email, principal.ExternalId);
        var displayName = Coalesce(principal.DisplayName, email);

        var user = await users.FindByExternalIdAsync(principal.ExternalId, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            user = new User(Guid.CreateVersion7(), principal.ExternalId, email, displayName);
            await users.AddAsync(user, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return user;
        }

        if (!string.Equals(user.Email, email, StringComparison.Ordinal) ||
            !string.Equals(user.DisplayName, displayName, StringComparison.Ordinal))
        {
            user.SyncProfile(email, displayName);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return user;
    }

    private static string Coalesce(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
