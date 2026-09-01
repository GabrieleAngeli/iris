using Iris.Contracts.Access;

namespace Iris.App.Services;

public interface IAuthService
{
	bool IsAuthenticated { get; }
	string? CurrentUser { get; }

	/// <summary>Identity + permissions returned by the API for the signed-in user.</summary>
	MeResponse? Me { get; }

	/// <summary>
	/// Dev-mode sign-in: the user name is an email that must match a configured
	/// <c>Iris:Auth:DevUsers</c> entry on the API. The password is ignored in dev mode.
	/// </summary>
	Task<AuthResult> SignInAsync(string username, string password, CancellationToken ct = default);

	/// <summary>Signs in with Microsoft 365 / Entra ID single sign-on.</summary>
	Task<AuthResult> SignInWithSsoAsync(CancellationToken ct = default);

	void SignOut();

	/// <summary>Raised after a successful sign-in or sign-out — UI bound to <see cref="Me"/>/<see cref="CurrentUser"/> should refresh.</summary>
	event EventHandler? StateChanged;
}

public readonly record struct AuthResult(bool Success, string? Error);

public sealed class AuthService(IIrisApiClient api, IEntraIdAuthenticator entraId) : IAuthService
{
	public bool IsAuthenticated { get; private set; }
	public string? CurrentUser { get; private set; }
	public MeResponse? Me { get; private set; }

	public event EventHandler? StateChanged;

	public async Task<AuthResult> SignInAsync(string username, string password, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(username))
		{
			return new AuthResult(false, "Enter your user name or email.");
		}

		api.DevUserEmail = username.Trim();

		try
		{
			var me = await api.GetMeAsync(ct);
			if (me is null)
			{
				api.DevUserEmail = null;
				return new AuthResult(false,
					"The Iris API rejected this user. Use a configured dev user, e.g. admin@iris.local.");
			}

			return ApplySignedInUser(me);
		}
		catch (HttpRequestException ex)
		{
			api.DevUserEmail = null;
			return new AuthResult(false, $"Cannot reach the Iris API. Is it running? ({ex.Message})");
		}
		catch (TaskCanceledException)
		{
			api.DevUserEmail = null;
			return new AuthResult(false, "The Iris API did not respond in time.");
		}
	}

	public async Task<AuthResult> SignInWithSsoAsync(CancellationToken ct = default)
	{
		var signIn = await entraId.SignInAsync(ct);
		if (!signIn.Success)
		{
			return new AuthResult(false, signIn.Error);
		}

		api.BearerToken = signIn.AccessToken;

		try
		{
			var me = await api.GetMeAsync(ct);
			if (me is null)
			{
				api.BearerToken = null;
				return new AuthResult(false, "Signed in with Microsoft 365, but the Iris API rejected this account.");
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

	public void SignOut()
	{
		IsAuthenticated = false;
		CurrentUser = null;
		Me = null;
		api.DevUserEmail = null;
		api.BearerToken = null;
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
