using Iris.Contracts.Access;

namespace Iris.App.Services;

public interface IAuthService
{
	bool IsAuthenticated { get; }
	string? CurrentUser { get; }

	/// <summary>Identity + permissions returned by the API for the signed-in user.</summary>
	MeResponse? Me { get; }

	/// <summary>
	/// Signs in with a local email and password — for anyone without an SSO platform to lean on.
	/// Verified against a real account; issues a session token used on every later request.
	/// </summary>
	Task<AuthResult> SignInAsync(string username, string password, bool rememberMe = false, CancellationToken ct = default);

	Task<AuthResult> TryResumeRememberedSessionAsync(CancellationToken ct = default);

	/// <summary>Carries a freshly set local password on subsequent dev-mode calls (used by the first-login step).</summary>
	void UseLocalPassword(string password);

	/// <summary>Signs in with Microsoft 365 / Entra ID single sign-on.</summary>
	Task<AuthResult> SignInWithSsoAsync(CancellationToken ct = default);

	/// <summary>
	/// Applies an already-issued bearer session token (from a local login or the setup wizard,
	/// which signs the new super-admin straight in) — fetches <see cref="Me"/> and updates
	/// authenticated state, same as every other sign-in path.
	/// </summary>
	Task<AuthResult> ApplySessionAsync(string token, CancellationToken ct = default);

	void SignOut();

	/// <summary>Raised after a successful sign-in or sign-out — UI bound to <see cref="Me"/>/<see cref="CurrentUser"/> should refresh.</summary>
	event EventHandler? StateChanged;
}

public readonly record struct AuthResult(bool Success, string? Error);

public sealed class AuthService(
	IIrisApiClient api,
	IEntraIdAuthenticator entraId,
	IAppPreferenceService preferences) : IAuthService
{
	public bool IsAuthenticated { get; private set; }
	public string? CurrentUser { get; private set; }
	public MeResponse? Me { get; private set; }

	public event EventHandler? StateChanged;

	public async Task<AuthResult> SignInAsync(string username, string password, bool rememberMe = false, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(username))
		{
			return new AuthResult(false, "Enter your user name or email.");
		}

		if (string.IsNullOrEmpty(password))
		{
			return new AuthResult(false, "Enter your password.");
		}

		string token;
		try
		{
			var login = await api.LoginAsync(new LoginRequest(username.Trim(), password), ct);
			token = login.Token;
		}
		catch (IrisApiException ex)
		{
			return new AuthResult(false, ex.Message);
		}
		catch (HttpRequestException ex)
		{
			return new AuthResult(false, $"Cannot reach the Iris API. Is it running? ({ex.Message})");
		}
		catch (TaskCanceledException)
		{
			return new AuthResult(false, "The Iris API did not respond in time.");
		}

		var result = await ApplySessionAsync(token, ct);
		if (result.Success)
		{
			if (rememberMe)
			{
				await preferences.SetRememberedSessionTokenAsync(token).ConfigureAwait(false);
			}
			else
			{
				preferences.ClearRememberedSessionToken();
			}
		}

		return result;
	}

	public async Task<AuthResult> TryResumeRememberedSessionAsync(CancellationToken ct = default)
	{
		var token = await preferences.GetRememberedSessionTokenAsync().ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(token))
		{
			return new AuthResult(false, null);
		}

		var result = await ApplySessionAsync(token, ct);
		if (!result.Success)
		{
			preferences.ClearRememberedSessionToken();
		}

		return result;
	}

	public async Task<AuthResult> ApplySessionAsync(string token, CancellationToken ct = default)
	{
		api.BearerToken = token;

		try
		{
			var me = await api.GetMeAsync(ct);
			if (me is null)
			{
				api.BearerToken = null;
				return new AuthResult(false, "Signed in, but the Iris API rejected this session.");
			}

			return ApplySignedInUser(me);
		}
		catch (HttpRequestException ex)
		{
			api.BearerToken = null;
			return new AuthResult(false, $"Cannot reach the Iris API. Is it running? ({ex.Message})");
		}
		catch (TaskCanceledException)
		{
			api.BearerToken = null;
			return new AuthResult(false, "The Iris API did not respond in time.");
		}
	}

	public void UseLocalPassword(string password) =>
		api.DevUserPassword = string.IsNullOrEmpty(password) ? null : password;

	public async Task<AuthResult> SignInWithSsoAsync(CancellationToken ct = default)
	{
		var signIn = await entraId.SignInAsync(ct);
		if (!signIn.Success)
		{
			return new AuthResult(false, signIn.Error);
		}

		api.BearerToken = signIn.AccessToken;
		api.DevUserPassword = null;

		try
		{
			var setup = await api.GetSetupStatusAsync(ct);
			if (setup.NeedsSetup)
			{
				await api.ClaimSetupAdminAsync(ct);
			}

			var me = await api.GetMeAsync(ct);
			if (me is null)
			{
				api.BearerToken = null;
				return new AuthResult(false, "Signed in with Microsoft 365, but the Iris API rejected this account.");
			}

			return ApplySignedInUser(me);
		}
		catch (IrisApiException ex)
		{
			api.BearerToken = null;
			return new AuthResult(false, ex.Message);
		}
		catch (HttpRequestException ex)
		{
			api.BearerToken = null;
			return new AuthResult(false, $"Cannot reach the Iris API. Is it running? ({ex.Message})");
		}
		catch (TaskCanceledException)
		{
			api.BearerToken = null;
			return new AuthResult(false, "The Iris API did not respond in time.");
		}
	}

	public void SignOut()
	{
		IsAuthenticated = false;
		CurrentUser = null;
		Me = null;
		api.DevUserEmail = null;
		api.DevUserPassword = null;
		api.BearerToken = null;
		preferences.ClearRememberedSessionToken();
		_ = entraId.SignOutAsync();
		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	private AuthResult ApplySignedInUser(MeResponse me)
	{
		Me = me;
		CurrentUser = me.DisplayName;
		IsAuthenticated = true;
		StateChanged?.Invoke(this, EventArgs.Empty);
		return new AuthResult(true, null);
	}
}
