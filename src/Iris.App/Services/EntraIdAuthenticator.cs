using Microsoft.Identity.Client;

namespace Iris.App.Services;

/// <summary>Outcome of a Microsoft 365 sign-in attempt.</summary>
public readonly record struct EntraIdSignInResult(bool Success, string? AccessToken, string? Error);

public interface IEntraIdAuthenticator
{
	/// <summary>
	/// Signs the user in against the vendor's Microsoft 365 tenant: silently against any
	/// cached account first, falling back to an interactive prompt (Windows WAM broker,
	/// or MSAL's own dialog if WAM isn't available) otherwise.
	/// </summary>
	Task<EntraIdSignInResult> SignInAsync(CancellationToken cancellationToken = default);

	/// <summary>Clears every cached account from the token cache.</summary>
	Task SignOutAsync();
}

public sealed class EntraIdAuthenticator(
	IPublicClientApplication pca,
	EntraIdOptions options,
	IWindowHandleProvider windowHandleProvider) : IEntraIdAuthenticator
{
	private string[] Scopes => [options.ApiScope];

	public async Task<EntraIdSignInResult> SignInAsync(CancellationToken cancellationToken = default)
	{
		if (!options.IsConfigured)
		{
			return new EntraIdSignInResult(false, null,
				"Microsoft 365 sign-in isn't configured yet. Set TenantId/ClientId/ApiScope in EntraIdOptions " +
				"— see docs/entra-id-setup.md.");
		}

		try
		{
			var accounts = await pca.GetAccountsAsync().ConfigureAwait(false);
			var account = accounts.FirstOrDefault();

			AuthenticationResult result;
			try
			{
				// A cached account first; otherwise try the account the user is already
				// signed into Windows with (WAM ties into that without any prompt).
				result = await pca.AcquireTokenSilent(Scopes, account ?? PublicClientApplication.OperatingSystemAccount)
					.ExecuteAsync(cancellationToken)
					.ConfigureAwait(false);
			}
			catch (MsalUiRequiredException)
			{
				result = await pca.AcquireTokenInteractive(Scopes)
					.WithParentActivityOrWindow(windowHandleProvider.GetHandle())
					.ExecuteAsync(cancellationToken)
					.ConfigureAwait(false);
			}

			return new EntraIdSignInResult(true, result.AccessToken, null);
		}
		catch (MsalException ex)
		{
			return new EntraIdSignInResult(false, null, ex.Message);
		}
	}

	public async Task SignOutAsync()
	{
		var accounts = await pca.GetAccountsAsync().ConfigureAwait(false);
		foreach (var account in accounts)
		{
			await pca.RemoveAsync(account).ConfigureAwait(false);
		}
	}
}
