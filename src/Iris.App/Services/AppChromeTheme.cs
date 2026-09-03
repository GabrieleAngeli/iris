namespace Iris.App.Services;

public static class AppChromeTheme
{
	public static bool IsDark =>
		EffectiveTheme() == AppTheme.Dark;

	public static void ApplyToOpenShells()
	{
		var windows = Application.Current?.Windows;
		if (windows is null)
		{
			return;
		}

		foreach (var shell in windows.Select(window => window.Page).OfType<Shell>())
		{
			ApplyTo(shell);
		}
	}

	public static void ApplyTo(Shell shell)
	{
		var dark = IsDark;
		var top = ResourceColor(dark ? "PageTitleBarDark" : "PageTitleBarLight");
		var foreground = ResourceColor(dark ? "TextPrimaryDark" : "TextPrimaryLight");

		Shell.SetBackgroundColor(shell, top);
		Shell.SetForegroundColor(shell, foreground);
		Shell.SetTitleColor(shell, foreground);
	}

	private static AppTheme EffectiveTheme()
	{
		var app = Application.Current;
		if (app is null)
		{
			return AppTheme.Unspecified;
		}

		return app.UserAppTheme == AppTheme.Unspecified
			? app.RequestedTheme
			: app.UserAppTheme;
	}

	public static Color ResourceColor(string key) =>
		Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
			? color
			: Colors.Transparent;
}
