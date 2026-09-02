namespace Iris.Contracts.Setup;

/// <summary>Result of <c>GET /setup/status</c>.</summary>
public sealed record SetupStatusResponse(bool NeedsSetup);

/// <summary>The SMTP relay to send email through, collected in step 1 of the setup wizard.</summary>
public sealed record MailProviderInput(
    string SmtpHost,
    int SmtpPort,
    string? SmtpUsername,
    string? SmtpPassword,
    string FromAddress,
    string? FromDisplayName,
    bool EnableSsl);

/// <summary>
/// Body of <c>POST /setup/test-mail</c> — tries the given (not-necessarily-saved) settings by
/// actually connecting and sending a real email to <see cref="TestRecipient"/>.
/// </summary>
public sealed record TestMailConnectionRequest(MailProviderInput Mail, string TestRecipient);

/// <summary>Body of <c>POST /setup/complete</c> — the whole first-run wizard in one call.</summary>
public sealed record CompleteSetupRequest(
    MailProviderInput Mail,
    string AdminEmail,
    string AdminDisplayName,
    string AdminPassword);

/// <summary>
/// Result of <c>POST /setup/complete</c>. <see cref="Token"/> signs the new super-admin straight
/// in — no separate login step for the very first interaction with a fresh install.
/// </summary>
public sealed record CompleteSetupResponse(Guid UserId, string Email, string Token, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Result of <c>POST /setup/claim-admin</c>: the authenticated SSO identity that claimed the
/// first platform-admin role. No token is returned because the caller already has one.
/// </summary>
public sealed record ClaimSetupAdminResponse(Guid UserId, string Email, string DisplayName);
