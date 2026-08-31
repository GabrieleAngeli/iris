namespace Iris.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell())
		{
			Title = "Iris",
			Width = 1180,
			Height = 780,
			MinimumWidth = 900,
			MinimumHeight = 600
		};
	}
}
