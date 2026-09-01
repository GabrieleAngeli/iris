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

	/// <summary>
	/// How many times to retry the setup-status check on a connection failure, and how long to
	/// wait between attempts — smooths over the well-known race where the client (and this page)
	/// is up before the API has finished starting (see the "Iris (API + App)" launch compound).
	/// </summary>
	private const int SetupCheckRetries = 5;
	private static readonly TimeSpan SetupCheckRetryDelay = TimeSpan.FromSeconds(1);

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// Only worth checking once per app launch — once past it, setup can't become "needed"
		// again (see the replay guard in CompleteSetupHandler), so there's no point asking again.
		// Only latched on an actual answer from the API (success) — a connectivity failure must
		// not permanently suppress the check, or the wizard silently never shows once hit.
		if (_checkedSetup)
		{
			return;
		}

		for (var attempt = 1; attempt <= SetupCheckRetries; attempt++)
		{
			try
			{
				var status = await _api.GetSetupStatusAsync();
				_checkedSetup = true;

				if (status.NeedsSetup)
				{
					await Shell.Current.GoToAsync("//setup");
				}

				return;
			}
			catch (Exception ex) when (ex is IrisApiException or HttpRequestException or TaskCanceledException)
			{
				if (attempt == SetupCheckRetries)
				{
					// Still unreachable — let the operator try to sign in normally (which surfaces
					// the same connectivity error) rather than blocking the screen. _checkedSetup
					// stays false, so this runs again next time the page appears.
					return;
				}

				await Task.Delay(SetupCheckRetryDelay);
			}
		}
	}
}
