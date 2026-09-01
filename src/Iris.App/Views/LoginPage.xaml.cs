namespace Iris.App.Views;

public partial class LoginPage : ContentPage
{
	private readonly IIrisApiClient _api;
	private bool _checkedSetup;

	public LoginPage(LoginViewModel vm, IIrisApiClient api)
	{
		InitializeComponent();
		BindingContext = vm;
		_api = api;

		vm.AcceptInvitationRequested += async (_, _) => await Shell.Current.GoToAsync("//accept-invitation");
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// Only worth checking once per app launch — once past it, setup can't become "needed"
		// again (see the replay guard in CompleteSetupHandler), so there's no point asking again.
		if (_checkedSetup)
		{
			return;
		}

		_checkedSetup = true;

		try
		{
			var status = await _api.GetSetupStatusAsync();
			if (status.NeedsSetup)
			{
				await Shell.Current.GoToAsync("//setup");
			}
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException or TaskCanceledException)
		{
			// The API might just not be reachable yet — let the operator try to sign in normally
			// (which will surface the same connectivity error) rather than blocking the screen.
		}
	}
}
