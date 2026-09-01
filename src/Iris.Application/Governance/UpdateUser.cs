using Iris.Application.Abstractions;
using Iris.Application.Access;
using Iris.Application.Common;
using Iris.Contracts.Access;

namespace Iris.Application.Governance;

/// <summary>Command for <c>PUT /governance/users/{id}</c> — an admin editing a user's profile and active flag.</summary>
public sealed record UpdateUserCommand(Guid Id, string Email, string DisplayName, bool IsActive);

public sealed class UpdateUserHandler(
    IUserRepository users,
    IRoleAssignmentRepository assignments,
    IRoleRepository roles,
    IUnitOfWork unitOfWork)
{
    public async Task<UserResponse> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var email = command.Email?.Trim() ?? string.Empty;
        var displayName = command.DisplayName?.Trim() ?? string.Empty;

        if (email.Length == 0)
        {
            throw new ValidationException("Email is required.");
        }

        if (displayName.Length == 0)
        {
            throw new ValidationException("Display name is required.");
        }

        var user = await users.GetAsync(command.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("User", command.Id);

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await users.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
            if (clash is not null && clash.Id != user.Id)
            {
                throw new ConflictException($"A user with email '{email}' already exists.");
            }
        }

        user.SyncProfile(email, displayName);
        if (command.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var userAssignments = await assignments.GetForUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var rolesById = (await roles.GetAllAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Id);

        return UserMapping.ToResponse(user, userAssignments, rolesById);
    }
}
