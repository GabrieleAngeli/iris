using Microsoft.Extensions.DependencyInjection;

namespace Iris.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var shell = activationState!.Context.Services.GetRequiredService<AppShell>();

		return new Window(shell)
		{
			Title = "Iris",
			Width = 1180,
			Height = 780,
			MinimumWidth = 900,
			MinimumHeight = 600
		};
	}
}
