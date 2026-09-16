namespace Iris.Infrastructure.Integrations;

internal sealed class AwxOptions
{
    public string? Endpoint { get; set; }

    /// <summary>Access token value provided directly by config/env. When it's a UI-saved secret
    /// instead, <see cref="TokenSecretReference"/> points at it and <see cref="AwxClient"/>
    /// resolves the value lazily from <c>ISecretStore</c> on first use (so it works even when the
    /// secret only becomes available after an admin unlocks the fallback vault post-startup).</summary>
    public string? Token { get; set; }

    public string? TokenSecretReference { get; set; }

    public int? JobTemplateId { get; set; }

    /// <summary>Job template dedicated to server inventory discovery (<c>use_fact_cache</c> on),
    /// separate from <see cref="JobTemplateId"/> which deploys applications.</summary>
    public int? FactsJobTemplateId { get; set; }

    /// <summary>OAuth2 Application client id — set (with a refresh token, and a client secret for
    /// a Confidential application) to let <see cref="AwxClient"/> auto-renew the access token on a
    /// 401 instead of failing.</summary>
    public string? OAuthClientId { get; set; }

    /// <summary>Only set for a Confidential OAuth2 application; Public applications have none.</summary>
    public string? OAuthClientSecret { get; set; }

    public string? OAuthClientSecretReference { get; set; }

    public string? RefreshToken { get; set; }

    public string? RefreshTokenSecretReference { get; set; }

    /// <summary>Whether an access token is available now or resolvable later from a stored reference.</summary>
    public bool HasToken =>
        !string.IsNullOrWhiteSpace(Token) || !string.IsNullOrWhiteSpace(TokenSecretReference);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        HasToken &&
        JobTemplateId is > 0;

    /// <summary>Whether the OAuth2 refresh flow is configured — a client id + a refresh token
    /// (value or reference); the client secret is optional (absent for a Public application).</summary>
    public bool CanRefresh =>
        !string.IsNullOrWhiteSpace(OAuthClientId) &&
        (!string.IsNullOrWhiteSpace(RefreshToken) || !string.IsNullOrWhiteSpace(RefreshTokenSecretReference));
}
