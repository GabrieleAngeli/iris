using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace Iris.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
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

		// ---- Services ---------------------------------------------
		builder.Services.AddSingleton<IAuthService, AuthService>();
		builder.Services.AddSingleton<IDashboardDataService, DashboardDataService>();

		// ---- View models ----------------------------------------
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<ComponentsViewModel>();
		builder.Services.AddTransient<AccessViewModel>();

		// ---- Pages ----------------------------------------------
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<ComponentsPage>();
		builder.Services.AddTransient<AccessPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
