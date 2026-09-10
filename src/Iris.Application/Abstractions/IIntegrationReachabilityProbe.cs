namespace Iris.Application.Abstractions;

/// <summary>Which phase of an integration probe failed.</summary>
public enum IntegrationTestStage
{
    /// <summary>Could not reach the endpoint at all (DNS, TCP, TLS, timeout).</summary>
    Connect,

    /// <summary>Reached the service, but it rejected the supplied token.</summary>
    Authenticate,
}

/// <summary>Raised by <see cref="IIntegrationReachabilityProbe"/>; <see cref="Stage"/> says which phase failed.</summary>
public sealed class IntegrationConnectionException(IntegrationTestStage stage, string message) : Exception(message)
{
    public IntegrationTestStage Stage { get; } = stage;
}

/// <summary>
/// Result of an AWX probe. When the OAuth2 refresh flow was exercised (client id/secret +
/// refresh token supplied), the probe performs the first refresh and returns the freshly-minted
/// pair — AWX rotates the refresh token, so these are what must be persisted, not what the
/// operator typed. Both null when only reachability / a static token was checked.
/// </summary>
public sealed record AwxProbeResult(string? RefreshedAccessToken, string? RefreshedRefreshToken);

/// <summary>
/// Checks an OpenBao/AWX endpoint (and, when given, its token / OAuth2 refresh credentials)
/// against settings that may not be saved yet — used by the first-run setup wizard to validate
/// before persisting, exactly like <see cref="IEmailSender.TestConnectionAsync"/> does for SMTP.
/// Throws <see cref="IntegrationConnectionException"/> on any failure; never partially "succeeds".
/// </summary>
public interface IIntegrationReachabilityProbe
{
    Task ProbeOpenBaoAsync(string endpoint, string? token, CancellationToken cancellationToken = default);

    Task<AwxProbeResult> ProbeAwxAsync(
        string endpoint,
        string? token,
        string? oAuthClientId,
        string? oAuthClientSecret,
        string? refreshToken,
        CancellationToken cancellationToken = default);
}
