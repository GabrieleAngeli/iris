using Iris.Contracts.Setup;

namespace Iris.App.ViewModels;

/// <summary>
/// First-run wizard: steps 1-2 collect OpenBao/AWX configuration or intent, step 3 configures
/// the mail relay Iris sends invitations through, step 4 creates the first super-admin. Runs
/// once — <see cref="LoginPage"/> only routes here while <c>GetSetupStatusAsync</c> says it's
/// still needed. On success the new admin is signed in straight to the dashboard, no separate
/// login step.
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

	public bool IsOpenBaoStep => CurrentStep == 1;

	public bool IsAwxStep => CurrentStep == 2;

	public bool IsMailStep => CurrentStep == 3;

	public bool IsAdminStep => CurrentStep == 4;

	partial void OnCurrentStepChanged(int value)
	{
		OnPropertyChanged(nameof(IsOpenBaoStep));
		OnPropertyChanged(nameof(IsAwxStep));
		OnPropertyChanged(nameof(IsMailStep));
		OnPropertyChanged(nameof(IsAdminStep));
	}

	// ----- Step 1: OpenBao -----

	/// <summary>Options shown in the OpenBao/AWX mode <c>Picker</c>s — same three choices for
	/// both, index-matched to <see cref="OpenBaoModeIndex"/>/<see cref="AwxModeIndex"/>
	/// (0 = use existing, 1 = install for me, 2 = skip for now).</summary>
	public IReadOnlyList<string> IntegrationModeOptions { get; } =
	[
		"Use an existing instance",
		"Install/start one for me",
		"Skip for now — configure later in System settings",
	];

	private const int ModeUseExisting = 0;
	private const int ModeInstallForMe = 1;

	[ObservableProperty] private int _openBaoModeIndex;
	[ObservableProperty] private string _openBaoEndpoint = string.Empty;
	[ObservableProperty] private string _openBaoToken = string.Empty;
	[ObservableProperty] private string? _openBaoError;

	public bool IsOpenBaoUseExisting => OpenBaoModeIndex == ModeUseExisting;

	public bool HasOpenBaoError => !string.IsNullOrEmpty(OpenBaoError);

	partial void OnOpenBaoModeIndexChanged(int value) => OnPropertyChanged(nameof(IsOpenBaoUseExisting));

	partial void OnOpenBaoErrorChanged(string? value) => OnPropertyChanged(nameof(HasOpenBaoError));

	[RelayCommand]
	private void GoToAwxStep()
	{
		if (OpenBaoModeIndex == ModeUseExisting && string.IsNullOrWhiteSpace(OpenBaoEndpoint))
		{
			OpenBaoError = "Enter the OpenBao endpoint, or choose a different option.";
			return;
		}

		OpenBaoError = null;
		CurrentStep = 2;
	}

	[RelayCommand]
	private void BackToOpenBaoStep() => CurrentStep = 1;

	// ----- Step 2: AWX -----

	[ObservableProperty] private int _awxModeIndex;
	[ObservableProperty] private string _awxEndpoint = string.Empty;
	[ObservableProperty] private string _awxToken = string.Empty;
	[ObservableProperty] private string _awxJobTemplateId = string.Empty;
	[ObservableProperty] private string? _awxError;

	public bool IsAwxUseExisting => AwxModeIndex == ModeUseExisting;

	public bool HasAwxError => !string.IsNullOrEmpty(AwxError);

	partial void OnAwxModeIndexChanged(int value) => OnPropertyChanged(nameof(IsAwxUseExisting));

	partial void OnAwxErrorChanged(string? value) => OnPropertyChanged(nameof(HasAwxError));

	[RelayCommand]
	private void GoToMailStep()
	{
		if (AwxModeIndex == ModeUseExisting && string.IsNullOrWhiteSpace(AwxEndpoint))
		{
			AwxError = "Enter the AWX endpoint, or choose a different option.";
			return;
		}

		AwxError = null;
		CurrentStep = 3;
	}

	[RelayCommand]
	private void BackToAwxStep() => CurrentStep = 2;

	// ----- Step 3: mail provider -----

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
		CurrentStep = 4;
	}

	[RelayCommand]
	private void BackToMailStep() => CurrentStep = 3;

	// ----- Step 4: super-admin -----

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

			var openBao = OpenBaoModeIndex switch
			{
				ModeUseExisting => new OpenBaoSetupInput(
					Skip: false, InstallForMe: false,
					Endpoint: OpenBaoEndpoint.Trim(),
					Token: string.IsNullOrEmpty(OpenBaoToken) ? null : OpenBaoToken),
				ModeInstallForMe => new OpenBaoSetupInput(Skip: false, InstallForMe: true, Endpoint: null, Token: null),
				_ => new OpenBaoSetupInput(Skip: true, InstallForMe: false, Endpoint: null, Token: null),
			};

			var awxJobTemplateId = int.TryParse(AwxJobTemplateId, out var parsedJobTemplateId) ? parsedJobTemplateId : (int?)null;
			var awx = AwxModeIndex switch
			{
				ModeUseExisting => new AwxSetupInput(
					Skip: false, InstallForMe: false,
					Endpoint: AwxEndpoint.Trim(),
					Token: string.IsNullOrEmpty(AwxToken) ? null : AwxToken,
					JobTemplateId: awxJobTemplateId),
				ModeInstallForMe => new AwxSetupInput(Skip: false, InstallForMe: true, Endpoint: null, Token: null, JobTemplateId: null),
				_ => new AwxSetupInput(Skip: true, InstallForMe: false, Endpoint: null, Token: null, JobTemplateId: null),
			};

			var result = await _api.CompleteSetupAsync(new CompleteSetupRequest(
				mail, AdminEmail.Trim(), AdminDisplayName.Trim(), AdminPassword, openBao, awx));

			var signedIn = await _auth.ApplySessionAsync(result.Token);
			if (!signedIn.Success)
			{
				AdminError = signedIn.Error;
				return;
			}

			// Phase 2/3 hand-off point: once the platform.admin-gated provisioning endpoints
			// exist, call them here (POST /system/integrations/openbao/provision,
			// /awx/provision) when the corresponding flag is set — never from the anonymous
			// /setup/complete call itself. For now this is intentionally a no-op; the operator
			// can configure/provision OpenBao/AWX later from System settings.
			_ = result.OpenBaoProvisionRequested;
			_ = result.AwxProvisionRequested;

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
