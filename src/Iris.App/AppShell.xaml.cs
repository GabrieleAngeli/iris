using Iris.App.ViewModels;

namespace Iris.App;

public partial class AppShell : Shell
{
	private readonly AppShellViewModel _vm;
	private readonly IAuthService _auth;

	public AppShell(AppShellViewModel vm, IAuthService auth)
	{
		InitializeComponent();
		BindingContext = vm;
		_vm = vm;
		_auth = auth;
		Navigated += OnShellNavigated;

		// Detail routes reachable via GoToAsync.
		Routing.RegisterRoute("activitydetail", typeof(Views.ActivityDetailPage));

		// Always start on the bootstrap splash; it decides whether login is needed.
		CurrentItem = StartupContent;
	}

	private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		_vm.SetCurrentRoute(CurrentState?.Location.OriginalString);
	}

	private async void OnSignOutClicked(object? sender, EventArgs e)
	{
		bool confirmed = await DisplayAlert("Sign out", "Do you want to end this session?", "Sign out", "Cancel");
		if (!confirmed)
			return;

		FlyoutIsPresented = false;
		_auth.SignOut();
		await GoToAsync("//login");
	}
}
