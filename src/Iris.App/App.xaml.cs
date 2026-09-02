using Iris.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.App;

public partial class App : Application
{
	private readonly IAppPreferenceService _preferences;

	public App(IAppPreferenceService preferences)
	{
		_preferences = preferences;
		InitializeComponent();
		_preferences.ApplyTheme();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var services = activationState!.Context.Services;
		var shell = services.GetRequiredService<AppShell>();

		var window = new Window(shell)
		{
			Title = "Iris",
			Width = 1180,
			Height = 780,
			MinimumWidth = 900,
			MinimumHeight = 600,
		};

		// Restore the last position/size and keep persisting it as the window moves/resizes.
		services.GetRequiredService<INativeWindowConfigurator>().ConfigureMainWindow(window, "win.main");

		return window;
	}
}
