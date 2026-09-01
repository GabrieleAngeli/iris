using Iris.Application.Abstractions;
using Iris.Application.Common;

namespace Iris.Application.Access;

/// <summary>
/// <c>POST /auth/password</c>: the signed-in user sets (or changes) their local password — the
/// one they use when they sign in without SSO. Also clears the first-login "set a password" prompt.
/// </summary>
public sealed record SetMyPasswordCommand(string NewPassword, string? CurrentPassword);

public sealed class SetMyPasswordHandler(
    ICurrentUser currentUser,
    IUserProvisioningService provisioning,
    IPasswordHasher passwordHasher,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>Shortest password Iris will accept.</summary>
    public const int MinimumLength = 8;

    public async Task HandleAsync(SetMyPasswordCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var newPassword = command.NewPassword ?? string.Empty;
        if (newPassword.Length < MinimumLength)
        {
            throw new ValidationException($"The new password must be at least {MinimumLength} characters.");
        }

        var user = await provisioning.EnsureProvisionedAsync(currentUser, cancellationToken).ConfigureAwait(false);

        if (user.HasPassword)
        {
            if (string.IsNullOrEmpty(command.CurrentPassword) ||
                !passwordHasher.Verify(command.CurrentPassword, user.PasswordHash!))
            {
                throw new ValidationException("The current password is incorrect.");
            }
        }

        user.SetPassword(passwordHasher.Hash(newPassword), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary><c>POST /auth/password/skip</c>: the user declines to set a local password now; stop prompting.</summary>
public sealed record SkipMyPasswordSetupCommand;

public sealed class SkipMyPasswordSetupHandler(
    ICurrentUser currentUser,
    IUserProvisioningService provisioning,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(SkipMyPasswordSetupCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await provisioning.EnsureProvisionedAsync(currentUser, cancellationToken).ConfigureAwait(false);
        user.SkipPasswordSetup();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
