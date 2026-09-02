using Microsoft.Maui.Storage;

namespace Iris.App.Services;

public interface IAppPreferenceService
{
	string ThemeMode { get; set; }

	void ApplyTheme();

	Task<string?> GetRememberedSessionTokenAsync();

	Task SetRememberedSessionTokenAsync(string token);

	void ClearRememberedSessionToken();
}

public sealed class AppPreferenceService : IAppPreferenceService
{
	private const string ThemeKey = "ui.theme";
	private const string SessionTokenKey = "auth.sessionToken";

	public string ThemeMode
	{
		get => Preferences.Default.Get(ThemeKey, "System");
		set
		{
			var mode = value is "Light" or "Dark" ? value : "System";
			Preferences.Default.Set(ThemeKey, mode);
			ApplyTheme();
		}
	}

	public void ApplyTheme()
	{
		if (Application.Current is null)
		{
			return;
		}

		Application.Current.UserAppTheme = ThemeMode switch
		{
			"Light" => AppTheme.Light,
			"Dark" => AppTheme.Dark,
			_ => AppTheme.Unspecified,
		};
	}

	public async Task<string?> GetRememberedSessionTokenAsync()
	{
		try
		{
			return await SecureStorage.Default.GetAsync(SessionTokenKey).ConfigureAwait(false)
				?? FallbackToken();
		}
		catch
		{
			return FallbackToken();
		}
	}

	public async Task SetRememberedSessionTokenAsync(string token)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			ClearRememberedSessionToken();
			return;
		}

		try
		{
			await SecureStorage.Default.SetAsync(SessionTokenKey, token).ConfigureAwait(false);
			Preferences.Default.Remove(SessionTokenKey);
		}
		catch
		{
			Preferences.Default.Set(SessionTokenKey, token);
		}
	}

	public void ClearRememberedSessionToken()
	{
		SecureStorage.Default.Remove(SessionTokenKey);
		Preferences.Default.Remove(SessionTokenKey);
	}

private static string? FallbackToken() =>
	Preferences.Default.Get(SessionTokenKey, string.Empty) is { Length: > 0 } token ? token : null;
}
