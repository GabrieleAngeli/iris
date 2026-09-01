using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Access;
using Iris.Domain.Access;

namespace Iris.Application.Governance;

/// <summary>Command for <c>POST /governance/users</c>: pre-provisions a user ahead of their first sign-in.</summary>
public sealed record CreateUserCommand(string Email, string DisplayName);

public sealed class CreateUserHandler(IUserRepository users, IUnitOfWork unitOfWork)
{
    public async Task<UserResponse> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
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

        if (await users.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new ConflictException($"A user with email '{email}' already exists.");
        }

        var user = User.Invite(Guid.CreateVersion7(), email, displayName);
        await users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UserResponse(
            user.Id,
            user.ExternalId,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.IsProvisioned,
            []);
    }
}
