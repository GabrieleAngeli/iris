using Iris.Contracts.Setup;

namespace Iris.App.ViewModels;

/// <summary>
/// First-run wizard: step 1 configures the mail relay Iris sends invitations through, step 2
/// creates the first super-admin. Runs once — <see cref="LoginPage"/> only routes here while
/// <c>GetSetupStatusAsync</c> says it's still needed. On success the new admin is signed in
/// straight to the dashboard, no separate login step.
/// </summary>
public partial class SetupWizardViewModel : ObservableObject
{
	private const int MinimumPasswordLength = 8;

	private readonly IIrisApiClient _api;
	private readonly IAuthService _auth;

	public SetupWizardViewModel(IIrisApiClient api, IAuthService auth)
	{
		_api = api;
		_auth = auth;
	}

	[ObservableProperty] private int _currentStep = 1;

	public bool IsMailStep => CurrentStep == 1;

	public bool IsAdminStep => CurrentStep == 2;

	partial void OnCurrentStepChanged(int value)
	{
		OnPropertyChanged(nameof(IsMailStep));
		OnPropertyChanged(nameof(IsAdminStep));
	}

	// ----- Step 1: mail provider -----

	[ObservableProperty] private string _smtpHost = string.Empty;
	[ObservableProperty] private string _smtpPort = "587";
	[ObservableProperty] private string _smtpUsername = string.Empty;
	[ObservableProperty] private string _smtpPassword = string.Empty;
	[ObservableProperty] private string _fromAddress = string.Empty;
	[ObservableProperty] private string _fromDisplayName = string.Empty;
	[ObservableProperty] private bool _enableSsl = true;
	[ObservableProperty] private string? _mailError;

	public bool HasMailError => !string.IsNullOrEmpty(MailError);

	partial void OnMailErrorChanged(string? value) => OnPropertyChanged(nameof(HasMailError));

	[RelayCommand]
	private void GoToAdminStep()
	{
		if (string.IsNullOrWhiteSpace(SmtpHost))
		{
			MailError = "SMTP host is required.";
			return;
		}

		if (!int.TryParse(SmtpPort, out var port) || port is <= 0 or > 65535)
		{
			MailError = "Enter a valid SMTP port (1-65535).";
			return;
		}

		if (string.IsNullOrWhiteSpace(FromAddress))
		{
			MailError = "A \"from\" address is required.";
			return;
		}

		MailError = null;
		CurrentStep = 2;
	}

	[RelayCommand]
	private void BackToMailStep() => CurrentStep = 1;

	// ----- Step 2: super-admin -----

	[ObservableProperty] private string _adminEmail = string.Empty;
	[ObservableProperty] private string _adminDisplayName = string.Empty;
	[ObservableProperty] private string _adminPassword = string.Empty;
	[ObservableProperty] private string _confirmPassword = string.Empty;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _adminError;

	public bool HasAdminError => !string.IsNullOrEmpty(AdminError);

	partial void OnAdminErrorChanged(string? value) => OnPropertyChanged(nameof(HasAdminError));

	partial void OnIsBusyChanged(bool value) => CompleteCommand.NotifyCanExecuteChanged();

	/// <summary>Raised once setup completes — the page has already been signed straight in.</summary>
	public event EventHandler? Completed;

	[RelayCommand(CanExecute = nameof(NotBusy))]
	private async Task CompleteAsync()
	{
		if (string.IsNullOrWhiteSpace(AdminEmail))
		{
			AdminError = "Administrator email is required.";
			return;
		}

		if (string.IsNullOrWhiteSpace(AdminDisplayName))
		{
			AdminError = "Administrator name is required.";
			return;
		}

		if (AdminPassword.Length < MinimumPasswordLength)
		{
			AdminError = $"Use at least {MinimumPasswordLength} characters.";
			return;
		}

		if (!string.Equals(AdminPassword, ConfirmPassword, StringComparison.Ordinal))
		{
			AdminError = "The two passwords don't match.";
			return;
		}

		IsBusy = true;
		AdminError = null;

		try
		{
			var port = int.Parse(SmtpPort);
			var mail = new MailProviderInput(
				SmtpHost.Trim(),
				port,
				string.IsNullOrWhiteSpace(SmtpUsername) ? null : SmtpUsername.Trim(),
				string.IsNullOrEmpty(SmtpPassword) ? null : SmtpPassword,
				FromAddress.Trim(),
				string.IsNullOrWhiteSpace(FromDisplayName) ? null : FromDisplayName.Trim(),
				EnableSsl);

			var result = await _api.CompleteSetupAsync(new CompleteSetupRequest(
				mail, AdminEmail.Trim(), AdminDisplayName.Trim(), AdminPassword));

			var signedIn = await _auth.ApplySessionAsync(result.Token);
			if (!signedIn.Success)
			{
				AdminError = signedIn.Error;
				return;
			}

			AdminPassword = string.Empty;
			ConfirmPassword = string.Empty;
			Completed?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			AdminError = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool NotBusy => !IsBusy;
}
