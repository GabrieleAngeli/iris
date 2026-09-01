using Iris.Application.Abstractions;
using Iris.Application.Common;
using Iris.Contracts.Access;

namespace Iris.Application.Access;

/// <summary>
/// Command for <c>POST /auth/login</c> — production sign-in for users without an SSO platform to
/// lean on: real credentials verified against real accounts, a session token issued once and
/// reused (not sent on every request, unlike the dev-header shortcut).
/// </summary>
public sealed record LoginCommand(string Email, string Password);

public sealed class LoginHandler(
    IUserRepository users,
    SessionIssuer sessionIssuer,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
{
    /// <summary>How long a freshly issued session stays valid before sign-in is required again.</summary>
    public static readonly TimeSpan SessionLifetime = SessionIssuer.SessionLifetime;

    private const string InvalidCredentials = "Invalid email or password.";

    public async Task<LoginResponse> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var email = command.Email?.Trim() ?? string.Empty;
        if (email.Length == 0 || string.IsNullOrEmpty(command.Password))
        {
            throw new ValidationException(InvalidCredentials);
        }

        var user = await users.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            throw new ValidationException(InvalidCredentials);
        }

        if (!user.IsActive)
        {
            throw new ValidationException("This account is deactivated.");
        }

        if (!user.HasPassword)
        {
            throw new ValidationException(
                "This account has no local password yet. Use single sign-on, or accept your invitation email to set one.");
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash!))
        {
            throw new ValidationException(InvalidCredentials);
        }

        var (rawToken, expiresAtUtc) = await sessionIssuer.IssueAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new LoginResponse(rawToken, expiresAtUtc);
    }
}
