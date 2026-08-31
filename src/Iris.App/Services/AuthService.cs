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

	void SignOut();
}

public readonly record struct AuthResult(bool Success, string? Error);

public sealed class AuthService(IIrisApiClient api) : IAuthService
{
	public bool IsAuthenticated { get; private set; }
	public string? CurrentUser { get; private set; }
	public MeResponse? Me { get; private set; }

	public async Task<AuthResult> SignInAsync(string username, string password, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(username))
		{
			return new AuthResult(false, "Enter your user name or email.");
		}

		var email = username.Trim();

		try
		{
			var me = await api.GetMeAsync(email, ct);
			if (me is null)
			{
				return new AuthResult(false,
					"The Iris API rejected this user. Use a configured dev user, e.g. admin@iris.local.");
			}

			Me = me;
			CurrentUser = me.DisplayName;
			IsAuthenticated = true;
			api.DevUserEmail = email;
			return new AuthResult(true, null);
		}
		catch (HttpRequestException ex)
		{
			return new AuthResult(false, $"Cannot reach the Iris API. Is it running? ({ex.Message})");
		}
		catch (TaskCanceledException)
		{
			return new AuthResult(false, "The Iris API did not respond in time.");
		}
	}

	public void SignOut()
	{
		IsAuthenticated = false;
		CurrentUser = null;
		Me = null;
		api.DevUserEmail = null;
	}
}
