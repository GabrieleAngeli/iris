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

/// <summary>
/// OpenBao/AWX intent collected in the wizard's new steps, before the mail relay step.
/// <see cref="InstallForMe"/> only records the operator's *intent* — the anonymous setup
/// endpoint never itself provisions anything (that needs a `platform.admin`-gated call, made
/// by the client after sign-in, once that capability exists). <see cref="Endpoint"/>/
/// <see cref="Token"/> are only used when neither <see cref="Skip"/> nor
/// <see cref="InstallForMe"/> is set — i.e. "use an instance I already have."
/// </summary>
public sealed record OpenBaoSetupInput(bool Skip, bool InstallForMe, string? Endpoint, string? Token);

/// <summary>Same shape as <see cref="OpenBaoSetupInput"/>, plus the AWX job template id and the
/// optional OAuth2 refresh credentials (client id/secret + refresh token) that let Iris
/// auto-renew the access token.</summary>
public sealed record AwxSetupInput(
    bool Skip,
    bool InstallForMe,
    string? Endpoint,
    string? Token,
    int? JobTemplateId,
    string? OAuthClientId = null,
    string? OAuthClientSecret = null,
    string? RefreshToken = null);

/// <summary>
/// Body of <c>POST /setup/complete</c> — the whole first-run wizard in one call.
/// <see cref="OpenBao"/>/<see cref="Awx"/> default to <c>null</c> (treated as "skip") so
/// existing callers that predate these two wizard steps keep compiling/working unchanged.
/// </summary>
public sealed record CompleteSetupRequest(
    MailProviderInput Mail,
    string AdminEmail,
    string AdminDisplayName,
    string AdminPassword,
    OpenBaoSetupInput? OpenBao = null,
    AwxSetupInput? Awx = null);

/// <summary>
/// Result of <c>POST /setup/complete</c>. <see cref="Token"/> signs the new super-admin straight
/// in — no separate login step for the very first interaction with a fresh install.
/// <see cref="OpenBaoProvisionRequested"/>/<see cref="AwxProvisionRequested"/> tell the client
/// whether it should call the (not-yet-built) authenticated provisioning endpoint right after
/// signing in with <see cref="Token"/>.
/// </summary>
public sealed record CompleteSetupResponse(
    Guid UserId,
    string Email,
    string Token,
    DateTimeOffset ExpiresAtUtc,
    bool OpenBaoProvisionRequested = false,
    bool AwxProvisionRequested = false);

/// <summary>
/// Result of <c>POST /setup/claim-admin</c>: the authenticated SSO identity that claimed the
/// first platform-admin role. No token is returned because the caller already has one.
/// </summary>
public sealed record ClaimSetupAdminResponse(Guid UserId, string Email, string DisplayName);
