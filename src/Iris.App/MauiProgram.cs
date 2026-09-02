using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Serilog;
using Serilog.Formatting.Compact;

namespace Iris.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
#if WINDOWS
		WindowsInputStyling.Apply();
#endif

		// Serilog is the provider; ILogger<T> stays the abstraction, same as the backend.
		// A desktop app isn't always online, so the only sink for now is a local rolling
		// file — structured (compact JSON) so it's ready for a future remote shipper without
		// changing how anything here logs, only where the file ends up going.
		var logsPath = Path.Combine(FileSystem.AppDataDirectory, "logs", "iris-app-.log");
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Information()
			.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
			.Enrich.FromLogContext()
			.WriteTo.File(new CompactJsonFormatter(), logsPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
			.CreateLogger();

		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
			Log.Fatal(e.ExceptionObject as Exception, "Unhandled AppDomain exception");
		TaskScheduler.UnobservedTaskException += (_, e) =>
		{
			Log.Error(e.Exception, "Unobserved task exception");
			e.SetObserved();
		};

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// ---- API client --------------------------------------------
		builder.Services.AddSingleton(new IrisApiOptions());
		builder.Services.AddSingleton<IIrisApiClient>(sp =>
		{
			var options = sp.GetRequiredService<IrisApiOptions>();
			var http = new HttpClient { BaseAddress = new Uri(options.BaseUrl) };
			return new IrisApiClient(http);
		});

		// ---- Microsoft 365 / Entra ID single sign-on ---------------
		builder.Services.AddSingleton(new EntraIdOptions());
		builder.Services.AddSingleton<IPublicClientApplication>(sp =>
		{
			var options = sp.GetRequiredService<EntraIdOptions>();
			return PublicClientApplicationBuilder
				.Create(options.ClientId)
				.WithAuthority(options.Authority)
				.WithDefaultRedirectUri()
				.WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows))
				.Build();
		});
		builder.Services.AddSingleton<IEntraIdAuthenticator, EntraIdAuthenticator>();
#if WINDOWS
		builder.Services.AddSingleton<IWindowHandleProvider, WindowHandleProvider>();
#endif

		// ---- Windowing (geometry persistence + real modal dialog windows) ----
		builder.Services.AddSingleton<WindowGeometryStore>();
#if WINDOWS
		builder.Services.AddSingleton<INativeWindowConfigurator, NativeWindowConfigurator>();
#else
		builder.Services.AddSingleton<INativeWindowConfigurator, NullNativeWindowConfigurator>();
#endif
		builder.Services.AddSingleton<IDialogService, DialogService>();

		// ---- Services ---------------------------------------------
		builder.Services.AddSingleton<IAuthService, AuthService>();
		builder.Services.AddSingleton<IDashboardDataService, DashboardDataService>();

		// ---- Shell (one per app lifetime — resolved in App.CreateWindow) ----
		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddSingleton<AppShellViewModel>();

		// ---- View models ----------------------------------------
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<FirstLoginPasswordViewModel>();
		builder.Services.AddTransient<AcceptInvitationViewModel>();
		builder.Services.AddTransient<SetupWizardViewModel>();
		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<ComponentsViewModel>();
		builder.Services.AddTransient<AccessViewModel>();
		builder.Services.AddTransient<UsersViewModel>();
		builder.Services.AddTransient<CustomersViewModel>();
		builder.Services.AddTransient<ServersViewModel>();

		// ---- Pages ----------------------------------------------
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<FirstLoginPasswordPage>();
		builder.Services.AddTransient<AcceptInvitationPage>();
		builder.Services.AddTransient<SetupWizardPage>();
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<ComponentsPage>();
		builder.Services.AddTransient<AccessPage>();
		builder.Services.AddTransient<UsersPage>();
		builder.Services.AddTransient<CustomersPage>();
		builder.Services.AddTransient<ServersPage>();

		builder.Logging.AddSerilog(dispose: true);
#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
