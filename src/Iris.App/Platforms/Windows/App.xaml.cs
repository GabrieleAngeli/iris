using Microsoft.UI.Xaml;
using Serilog;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Iris.App.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		this.InitializeComponent();

		// UI-thread exceptions don't reach AppDomain.UnhandledException (wired in
		// MauiProgram) — they need catching here instead. Logged, not suppressed:
		// e.Handled is left false, so the app still crashes as it would without this hook.
		this.UnhandledException += (_, e) => Log.Fatal(e.Exception, "Unhandled WinUI exception");
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

