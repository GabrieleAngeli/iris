using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Application.Common;
using Iris.Contracts.Setup;
using Iris.Domain.Access;

namespace Iris.Application.Setup;

/// <summary>
/// Authenticated first-admin claim for SSO bootstrap: grants platform-admin to the current
/// identity, but only while setup is still incomplete and only for configured email addresses.
/// </summary>
public sealed record ClaimSetupAdminCommand(IReadOnlyList<string> AllowedEmails);

public sealed class ClaimSetupAdminHandler(
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    IUserProvisioningService provisioning,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<ClaimSetupAdminResponse> HandleAsync(
        ClaimSetupAdminCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var email = currentUser.Email?.Trim() ?? string.Empty;
        if (!currentUser.IsAuthenticated || email.Length == 0)
        {
            throw new ValidationException("An authenticated user with an email address is required to claim setup admin.");
        }

        var allowed = command.AllowedEmails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .ToArray();

        if (allowed.Length == 0)
        {
            throw new ForbiddenException("No setup admin claim emails are configured.");
        }

        if (!allowed.Contains(email, StringComparer.OrdinalIgnoreCase))
        {
            throw new ForbiddenException($"'{email}' is not allowed to claim the first platform administrator role.");
        }

        var platformAdminRole = await roles
            .GetByKeyAsync(GetSetupStatusHandler.PlatformAdminRoleKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The built-in role catalog has not been seeded yet - this should never happen after startup.");

        if (await assignments.ExistsForRoleAsync(platformAdminRole.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException("Setup has already been completed.");
        }

        var user = await provisioning.EnsureProvisionedAsync(currentUser, cancellationToken).ConfigureAwait(false);
        var assignment = new RoleAssignment(Guid.CreateVersion7(), user.Id, platformAdminRole.Id, AccessScope.Global());
        await assignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ClaimSetupAdminResponse(user.Id, user.Email, user.DisplayName);
    }
}
