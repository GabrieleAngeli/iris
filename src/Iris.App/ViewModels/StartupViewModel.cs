namespace Iris.App.ViewModels;

/// <summary>Launch bootstrap: setup detection and remembered-session restore before the login page can appear.</summary>
public partial class StartupViewModel(IIrisApiClient api, IAuthService auth) : ObservableObject
{
	private const int SetupCheckRetries = 5;
	private static readonly TimeSpan SetupCheckRetryDelay = TimeSpan.FromSeconds(1);

	private bool _started;

	[ObservableProperty] private string _statusMessage = "Starting Iris...";
	[ObservableProperty] private string? _errorMessage;

	public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

	partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

	public async Task StartAsync()
	{
		if (_started)
		{
			return;
		}

		_started = true;

		var setupState = await ResolveSetupAsync();
		if (setupState == StartupSetupState.NeedsSetup)
		{
			await Shell.Current.GoToAsync("//setup");
			return;
		}

		if (setupState == StartupSetupState.Unreachable)
		{
			await Shell.Current.GoToAsync("//login");
			return;
		}

		StatusMessage = "Restoring session...";
		var resume = await auth.TryResumeRememberedSessionAsync();
		if (resume.Success)
		{
			var next = auth.Me?.PasswordSetupPending == true ? "//first-login" : "//dashboard";
			await Shell.Current.GoToAsync(next);
			return;
		}

		await Shell.Current.GoToAsync("//login");
	}

	private async Task<StartupSetupState> ResolveSetupAsync()
	{
		StatusMessage = "Connecting to Iris...";
		ErrorMessage = null;

		for (var attempt = 1; attempt <= SetupCheckRetries; attempt++)
		{
			try
			{
				var status = await api.GetSetupStatusAsync();
				if (status.NeedsSetup)
				{
					return StartupSetupState.NeedsSetup;
				}

				return StartupSetupState.Ready;
			}
			catch (Exception ex) when (ex is IrisApiException or HttpRequestException or TaskCanceledException)
			{
				if (attempt == SetupCheckRetries)
				{
					ErrorMessage = "Cannot reach the Iris API.";
					return StartupSetupState.Unreachable;
				}

				await Task.Delay(SetupCheckRetryDelay);
			}
		}

		return StartupSetupState.Unreachable;
	}

	private enum StartupSetupState
	{
		Ready,
		NeedsSetup,
		Unreachable,
	}
}
