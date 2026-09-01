using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;

namespace Iris.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
#if WINDOWS
		WindowsInputStyling.Apply();
#endif

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
		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<ComponentsViewModel>();
		builder.Services.AddTransient<AccessViewModel>();
		builder.Services.AddTransient<UsersViewModel>();
		builder.Services.AddTransient<CustomersViewModel>();
		builder.Services.AddTransient<ServersViewModel>();

		// ---- Pages ----------------------------------------------
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<ComponentsPage>();
		builder.Services.AddTransient<AccessPage>();
		builder.Services.AddTransient<UsersPage>();
		builder.Services.AddTransient<CustomersPage>();
		builder.Services.AddTransient<ServersPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
