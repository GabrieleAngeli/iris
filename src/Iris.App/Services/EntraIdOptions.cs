namespace Iris.App.Services;

/// <summary>
/// Where the Iris desktop client's Microsoft 365 / Entra ID app registration lives and
/// which API scope it signs in for. Mirrors the placeholder-guard pattern used for
/// <c>AzureAd:TenantId</c>/<c>ClientId</c> in Iris.Api, but validated lazily — only when
/// the user actually attempts single sign-on — so dev-mode sign-in keeps working with
/// this left unconfigured. See docs/entra-id-setup.md for how to fill these in.
/// </summary>
public sealed class EntraIdOptions
{
	private const string PlaceholderGuid = "00000000-0000-0000-0000-000000000000";

	/// <summary>The vendor's own Microsoft 365 tenant. Single-tenant only — never "common"/"organizations".</summary>
	public string TenantId { get; set; } = PlaceholderGuid;

	/// <summary>Application (client) ID of the public client app registration for this desktop app.</summary>
	public string ClientId { get; set; } = PlaceholderGuid;

	/// <summary>Delegated scope exposed by the Iris API app registration, e.g. <c>api://&lt;api-client-id&gt;/access_as_user</c>.</summary>
	public string ApiScope { get; set; } = $"api://{PlaceholderGuid}/access_as_user";

	public string Authority => $"https://login.microsoftonline.com/{TenantId}";

	/// <summary>False while any value is still the unfilled placeholder — sign-in should refuse to start.</summary>
	public bool IsConfigured =>
		!IsPlaceholder(TenantId) && !IsPlaceholder(ClientId) && !ApiScope.Contains(PlaceholderGuid, StringComparison.Ordinal);

	private static bool IsPlaceholder(string value) =>
		string.IsNullOrWhiteSpace(value) || string.Equals(value, PlaceholderGuid, StringComparison.OrdinalIgnoreCase);
}
