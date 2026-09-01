using Iris.App.ViewModels;

namespace Iris.App;

public partial class AppShell : Shell
{
	public AppShell(AppShellViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;

		// Detail routes reachable via GoToAsync.
		Routing.RegisterRoute("activitydetail", typeof(Views.ActivityDetailPage));

		// Always start on the login screen.
		CurrentItem = LoginContent;
	}

	private async void OnSignOutClicked(object? sender, EventArgs e)
	{
		bool confirmed = await DisplayAlert("Sign out", "Do you want to end this session?", "Sign out", "Cancel");
		if (!confirmed)
			return;

		FlyoutIsPresented = false;
		await GoToAsync("//login");
	}
}
