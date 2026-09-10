using System.Net.Http.Headers;
using System.Text;

namespace Iris.Infrastructure.Integrations;

/// <summary>Shared shape of the AWX OAuth2 token-refresh request, used by both
/// <see cref="AwxClient"/> (runtime renewal) and <see cref="IntegrationReachabilityProbe"/>
/// (setup-wizard validation).</summary>
internal static class AwxOAuth
{
    /// <summary>
    /// Builds <c>POST {tokenUri}</c> with <c>grant_type=refresh_token</c>. A <b>Confidential</b>
    /// application (a client secret is set) authenticates with HTTP Basic, per the AWX docs; a
    /// <b>Public</b> application (no secret — AWX only hands out a client id + tokens) sends the
    /// client id in the form body instead.
    /// </summary>
    public static HttpRequestMessage BuildRefreshRequest(
        Uri tokenUri, string? clientId, string? clientSecret, string? refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, tokenUri);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken ?? string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }
        else
        {
            form["client_id"] = clientId ?? string.Empty;
        }

        request.Content = new FormUrlEncodedContent(form);
        return request;
    }

    /// <summary>Turns a non-2xx from <c>/api/o/token/</c> into an actionable message: 400 is a bad
    /// grant (the refresh token is invalid/expired/already used — AWX rotates it on every use),
    /// 401 is bad client authentication (wrong client id/secret, or Confidential vs Public).</summary>
    public static string DescribeRefreshFailure(int statusCode, string body)
    {
        if (statusCode == 401)
        {
            return "AWX rejected the OAuth2 client id/secret — check the Application's client id and secret, "
                + "and whether it's Confidential (needs a secret) or Public (no secret).";
        }

        if (statusCode == 400 || body.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
        {
            return "the AWX refresh token is invalid or was already used — AWX rotates it on every use. "
                + "Generate a new token in AWX (Users -> Tokens -> Add), don't test it elsewhere first, then re-enter it.";
        }

        return $"AWX rejected the OAuth2 token refresh ({statusCode}).";
    }
}
