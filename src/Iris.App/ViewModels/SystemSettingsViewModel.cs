using System.Collections.ObjectModel;
using Iris.Contracts.Audit;
using Iris.Contracts.Settings;
using Iris.Contracts.Setup;

namespace Iris.App.ViewModels;

public partial class SystemSettingsViewModel(
	IIrisApiClient api,
	IAppPreferenceService preferences) : ObservableObject
{
	private readonly string[] _themeModes = ["System", "Light", "Dark"];
	private readonly string[] _activityAreas = ["All", "Governance", "Infrastructure", "Applications", "Settings"];

	[ObservableProperty] private string _selectedThemeMode = preferences.ThemeMode;
	[ObservableProperty] private string _selectedActivityArea = "All";
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _error;
	[ObservableProperty] private bool _canManageSystem;
	[ObservableProperty] private bool _restartRequired;
	[ObservableProperty] private bool _fallbackSecretsPending;
	[ObservableProperty] private string _fallbackSecretsSummary = string.Empty;
	[ObservableProperty] private string _smtpSummary = "Not configured";
	[ObservableProperty] private bool _smtpConfigured;
	[ObservableProperty] private string _smtpHost = "-";
	[ObservableProperty] private string _smtpPort = "-";
	[ObservableProperty] private string _smtpUsername = "-";
	[ObservableProperty] private string _smtpFromAddress = "-";
	[ObservableProperty] private string _smtpFromDisplayName = "-";
	[ObservableProperty] private string _smtpEnableSsl = "-";

	public IReadOnlyList<string> ThemeModes => _themeModes;

	public IReadOnlyList<string> ActivityAreas => _activityAreas;

	public ObservableCollection<IntegrationConnectionRow> Integrations { get; } = [];

	public ObservableCollection<TransactionLogRow> Activity { get; } = [];

	/// <summary>Raised when the operator clicks "Configure" on an integration row — the page owns
	/// opening the dialog and reacting only after it has fully closed (see
	/// <see cref="IntegrationConnectionRow.ConfigureRequested"/> for why that ordering matters).
	/// The payload is one of <see cref="ConfigureOpenBaoDialogViewModel"/>/
	/// <see cref="ConfigureAwxDialogViewModel"/>/<see cref="ConfigureAnsibleDialogViewModel"/> —
	/// an <c>object</c> rather than three separate strongly-typed events, since the page needs
	/// to pattern-match on it anyway to pick which dialog view wraps it.</summary>
	public event EventHandler<object>? ConfigureRequested;

	/// <summary>Raised when the operator clicks "Edit" on the SMTP card — same rationale/pattern
	/// as <see cref="ConfigureRequested"/>, just not row-sourced since SMTP isn't one of the
	/// <see cref="Integrations"/> rows.</summary>
	public event EventHandler<ConfigureMailDialogViewModel>? ConfigureMailRequested;

	private MailProviderSettingsResponse? _mail;

	[RelayCommand(CanExecute = nameof(CanManageSystem))]
	private void ConfigureMail() =>
		ConfigureMailRequested?.Invoke(this, new ConfigureMailDialogViewModel(api, _mail));

	/// <summary>Raised when the operator clicks "Unlock" on the fallback-secrets banner — same
	/// wait-then-react pattern as every other dialog here.</summary>
	public event EventHandler<UnlockFallbackSecretsDialogViewModel>? UnlockFallbackSecretsRequested;

	[RelayCommand(CanExecute = nameof(CanManageSystem))]
	private void UnlockFallbackSecrets() =>
		UnlockFallbackSecretsRequested?.Invoke(this, new UnlockFallbackSecretsDialogViewModel(api));

	public bool HasError => !string.IsNullOrEmpty(Error);

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnSelectedThemeModeChanged(string value) =>
		preferences.ThemeMode = value;

	partial void OnSelectedActivityAreaChanged(string value)
	{
		if (CanManageSystem && RefreshActivityCommand.CanExecute(null))
		{
			RefreshActivityCommand.Execute(null);
		}
	}

	partial void OnIsBusyChanged(bool value)
	{
		LoadCommand.NotifyCanExecuteChanged();
		RefreshActivityCommand.NotifyCanExecuteChanged();
	}

	partial void OnCanManageSystemChanged(bool value)
	{
		ConfigureMailCommand.NotifyCanExecuteChanged();
		UnlockFallbackSecretsCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand(CanExecute = nameof(CanLoad))]
	private async Task LoadAsync()
	{
		IsBusy = true;
		Error = null;

		try
		{
			var settings = await api.GetSystemSettingsAsync();
			CanManageSystem = settings.CanManageSystem;
			RestartRequired = settings.RestartRequired;
			ApplyMail(settings.Mail);

			FallbackSecretsPending = settings.FallbackSecrets?.HasPendingWork == true;
			FallbackSecretsSummary = settings.FallbackSecrets is { } fallback
				? $"{fallback.PendingPersistCount} to save, {fallback.RestorableCount} to restore."
				: string.Empty;

			Integrations.Clear();
			foreach (var integration in settings.Integrations)
			{
				// Only openbao/awx/ansible ever have a real IIntegrationConnector registered
				// server-side (see RegisterIntegrations) — nexus/azure-devops are display-only
				// entries with no persisted settings and no way to actually reach them yet.
				// Offering Test/Configure for those would either 404 (misread by an operator as
				// "broken") or silently do nothing — reported as exactly that confusion
				// (2026-09-08: "nexus... test non restituisce nessun feedback"). Honest fix:
				// don't offer actions this build genuinely can't back.
				var hasRealConnector = integration.Key is "openbao" or "awx" or "ansible";
				var canManage = CanManageSystem && hasRealConnector;
				var canProvision = canManage && string.Equals(integration.Key, "openbao", StringComparison.OrdinalIgnoreCase);
				var row = new IntegrationConnectionRow(
					integration, TestIntegrationAsync, hasRealConnector, canProvision ? ProvisionOpenBaoAsync : null, canManage);
				row.Provisioned += async (_, _) => await LoadCommand.ExecuteAsync(null);
				row.ConfigureRequested += (_, _) =>
				{
					object? dialogVm = integration.Key.ToLowerInvariant() switch
					{
						"openbao" => new ConfigureOpenBaoDialogViewModel(api, integration.Endpoint),
						"awx" => new ConfigureAwxDialogViewModel(api, integration.Endpoint),
						"ansible" => new ConfigureAnsibleDialogViewModel(api, integration.Endpoint),
						_ => null,
					};
					if (dialogVm is not null)
					{
						ConfigureRequested?.Invoke(this, dialogVm);
					}
				};
				Integrations.Add(row);
			}

			await LoadActivityAsync();
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Error = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool CanLoad() => !IsBusy;

	private Task<IntegrationLinkResponse> TestIntegrationAsync(IntegrationConnectionRow row) =>
		api.GetIntegrationStatusAsync(row.Key, probe: true);

	private Task<ProvisionOpenBaoResponse> ProvisionOpenBaoAsync() => api.ProvisionOpenBaoAsync();

	[RelayCommand(CanExecute = nameof(CanLoad))]
	private async Task RefreshActivityAsync()
	{
		IsBusy = true;
		Error = null;

		try
		{
			await LoadActivityAsync();
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Error = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task LoadActivityAsync()
	{
		if (!CanManageSystem)
		{
			Activity.Clear();
			return;
		}

		var area = SelectedActivityArea == "All" ? null : SelectedActivityArea;
		var entries = await api.GetTransactionLogAsync(area, take: 50);

		Activity.Clear();
		foreach (var entry in entries)
		{
			Activity.Add(new TransactionLogRow(entry));
		}
	}

	private void ApplyMail(MailProviderSettingsResponse? mail)
	{
		_mail = mail;

		if (mail is null || !mail.IsConfigured)
		{
			SmtpConfigured = false;
			SmtpSummary = "Not configured";
			SmtpHost = "-";
			SmtpPort = "-";
			SmtpUsername = "-";
			SmtpFromAddress = "-";
			SmtpFromDisplayName = "-";
			SmtpEnableSsl = "-";
			return;
		}

		SmtpConfigured = true;
		SmtpSummary = $"{mail.FromAddress} via {mail.SmtpHost}:{mail.SmtpPort}";
		SmtpHost = mail.SmtpHost ?? "-";
		SmtpPort = mail.SmtpPort?.ToString() ?? "-";
		SmtpUsername = mail.SmtpUsername ?? "-";
		SmtpFromAddress = mail.FromAddress ?? "-";
		SmtpFromDisplayName = mail.FromDisplayName ?? "-";
		SmtpEnableSsl = mail.EnableSsl ? "Enabled" : "Disabled";
	}
}

public sealed partial class IntegrationConnectionRow : ObservableObject
{
	private readonly Func<IntegrationConnectionRow, Task<IntegrationLinkResponse>> _tester;
	private readonly Func<Task<ProvisionOpenBaoResponse>>? _provisioner;

	[ObservableProperty] private string _key;
	[ObservableProperty] private string _name;
	[ObservableProperty] private string _status;
	[ObservableProperty] private string _endpoint;
	[ObservableProperty] private string _message;
	[ObservableProperty] private string _checkedSummary = string.Empty;
	[ObservableProperty] private bool _isBusy;

	public IntegrationConnectionRow(
		IntegrationLinkResponse response,
		Func<IntegrationConnectionRow, Task<IntegrationLinkResponse>> tester,
		bool canTest,
		Func<Task<ProvisionOpenBaoResponse>>? provisioner = null,
		bool canConfigure = false)
	{
		_tester = tester;
		_provisioner = provisioner;
		CanTestAtAll = canTest;
		CanConfigure = canConfigure;
		_key = response.Key;
		_name = response.Name;
		_status = response.Status;
		_endpoint = string.IsNullOrWhiteSpace(response.Endpoint) ? "-" : response.Endpoint;
		_message = response.Message ?? string.Empty;
		_checkedSummary = Summarize(response.CheckedAtUtc);
	}

	/// <summary>False for integrations with no real <c>IIntegrationConnector</c> behind them
	/// (nexus/azure-devops today) — gates the "Test" button off entirely rather than letting it
	/// 404 and report a misleading "Unreachable" for something that was never actually checked.</summary>
	public bool CanTestAtAll { get; }

	/// <summary>Bound to the "Not available yet" label shown in place of the Test button — the
	/// inverse of <see cref="CanTestAtAll"/>, exposed as its own property since there is no
	/// inverted-bool XAML converter anywhere in this project (checked: none exists).</summary>
	public bool CannotTest => !CanTestAtAll;

	/// <summary>Gates the "Configure" button — lets the operator save an existing OpenBao
	/// endpoint/token at any time, not just from the once-only setup wizard. Raising
	/// <see cref="ConfigureRequested"/> and letting the *page* open the dialog (rather than
	/// awaiting a result here) matters: a dialog opened from inside a command handler that also
	/// tries to react to its own result before the window is confirmed closed is exactly the
	/// shape that caused a real crash earlier in this codebase (two windows racing each other) —
	/// see <c>SelectApplicationDialogViewModel</c>'s remarks in <c>DeploymentsViewModel.cs</c>.</summary>
	public bool CanConfigure { get; }

	/// <summary>Raised when "Configure" is clicked — carries no payload, the parent view model
	/// already has what it needs (this row's <see cref="Key"/>/<see cref="Endpoint"/>) to build
	/// the dialog view model itself.</summary>
	public event EventHandler? ConfigureRequested;

	[RelayCommand(CanExecute = nameof(CanConfigure))]
	private void Configure() => ConfigureRequested?.Invoke(this, EventArgs.Empty);

	/// <summary>Only true for the OpenBao row when the caller is platform.admin — gates the
	/// "Provision" button in XAML. Convenience/dev-mode only, see <c>ProvisionOpenBaoHandler</c>.</summary>
	public bool CanProvision => _provisioner is not null;

	public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

	/// <summary>When the background health check (see <c>IIntegrationHealthChecker</c>) has a
	/// real result for this row, "checked Xm ago" — requested by the user (2026-09-08): "un
	/// servizio che controlla i servizi connessi se sono raggiungibili e configurati
	/// correttamente." Empty when nothing has probed this row yet (e.g. "Pending restart", or
	/// the first background cycle hasn't run yet).</summary>
	public bool HasCheckedSummary => !string.IsNullOrEmpty(CheckedSummary);

	partial void OnCheckedSummaryChanged(string value) => OnPropertyChanged(nameof(HasCheckedSummary));

	/// <summary>Raised after a successful provision so the parent view model can reload
	/// (picks up the new endpoint/status and, most importantly, <c>RestartRequired</c>).</summary>
	public event EventHandler? Provisioned;

	partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

	partial void OnIsBusyChanged(bool value)
	{
		TestCommand.NotifyCanExecuteChanged();
		ProvisionCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand(CanExecute = nameof(CanTest))]
	private async Task TestAsync()
	{
		IsBusy = true;
		Status = "Testing...";
		Message = string.Empty;

		try
		{
			Apply(await _tester(this));
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Status = "Unreachable";
			Message = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool CanTest() => !IsBusy && CanTestAtAll;

	[RelayCommand(CanExecute = nameof(CanRunProvision))]
	private async Task ProvisionAsync()
	{
		if (_provisioner is null)
		{
			return;
		}

		IsBusy = true;
		Status = "Provisioning...";
		Message = string.Empty;

		try
		{
			var result = await _provisioner();
			Status = "Configured";
			Endpoint = result.Endpoint;
			Message = result.Message;
			Provisioned?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Status = "Unreachable";
			Message = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool CanRunProvision() => CanProvision && !IsBusy;

	private void Apply(IntegrationLinkResponse response)
	{
		Name = response.Name;
		Status = response.Status;
		Endpoint = string.IsNullOrWhiteSpace(response.Endpoint) ? "-" : response.Endpoint;
		Message = response.Message ?? string.Empty;
		CheckedSummary = Summarize(response.CheckedAtUtc);
	}

	private static string Summarize(DateTimeOffset? checkedAtUtc)
	{
		if (checkedAtUtc is not { } checkedAt)
		{
			return string.Empty;
		}

		var age = DateTimeOffset.UtcNow - checkedAt;
		var ago = age switch
		{
			{ TotalSeconds: < 60 } => "just now",
			{ TotalMinutes: < 60 } => $"{(int)age.TotalMinutes}m ago",
			{ TotalHours: < 24 } => $"{(int)age.TotalHours}h ago",
			_ => $"{(int)age.TotalDays}d ago",
		};
		return $"Checked {ago}";
	}
}

public sealed class TransactionLogRow(TransactionLogEntryResponse response)
{
	public string When => response.OccurredAtUtc.ToLocalTime().ToString("g");

	public string Area => response.Area;

	public string Action => response.Action;

	public string Actor => response.ActorDisplayName == response.ActorEmail
		? response.ActorEmail
		: $"{response.ActorDisplayName} ({response.ActorEmail})";

	public string Target => $"{response.EntityType} {response.EntityId}";

	public string Summary => response.Summary;
}

/// <summary>
/// Backs the "Configure OpenBao" dialog — the only place (besides the once-only setup wizard)
/// that saves an OpenBao endpoint/token via <c>PUT /system/integrations/openbao</c>. Prefills
/// <see cref="Endpoint"/> from what's currently shown; leaves <see cref="Token"/> blank, since
/// the server never echoes a saved token back and a blank token on save means "keep the
/// existing one" (see <c>SaveOpenBaoIntegrationSettingsHandler</c>).
///
/// Exposes a single <see cref="CloseRequested"/> signal, not separate confirmed/cancelled
/// events — the dialog's code-behind only closes on it, and the *page* checks
/// <see cref="WasSaved"/> only after <c>IDialogService.ShowAsync</c> has returned (i.e. the
/// window is verifiably closed) before reloading. See <c>SelectApplicationDialogViewModel</c>'s
/// remarks for why that ordering is load-bearing, not a style preference.
/// </summary>
public sealed partial class ConfigureOpenBaoDialogViewModel : ObservableObject
{
	private readonly IIrisApiClient _api;

	public ConfigureOpenBaoDialogViewModel(IIrisApiClient api, string? currentEndpoint)
	{
		_api = api;
		Endpoint = string.IsNullOrWhiteSpace(currentEndpoint) ? string.Empty : currentEndpoint;
	}

	[ObservableProperty] private string _endpoint;
	[ObservableProperty] private string _token = string.Empty;
	[ObservableProperty] private string _mountPath = "secret";
	[ObservableProperty] private bool _useKvV2 = true;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _error;

	public bool HasError => !string.IsNullOrEmpty(Error);

	public bool WasSaved { get; private set; }

	public event EventHandler? CloseRequested;

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnIsBusyChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

	[RelayCommand(CanExecute = nameof(CanSave))]
	private async Task SaveAsync()
	{
		if (string.IsNullOrWhiteSpace(Endpoint))
		{
			Error = "Enter the OpenBao endpoint.";
			return;
		}

		IsBusy = true;
		Error = null;

		try
		{
			await _api.SaveOpenBaoIntegrationSettingsAsync(new SaveOpenBaoIntegrationSettingsRequest(
				Endpoint.Trim(),
				string.IsNullOrEmpty(Token) ? null : Token,
				string.IsNullOrWhiteSpace(MountPath) ? "secret" : MountPath.Trim(),
				UseKvV2));
			WasSaved = true;
			CloseRequested?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Error = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool CanSave() => !IsBusy;

	[RelayCommand]
	private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>Backs the "Configure AWX" dialog — same shape/pattern as
/// <see cref="ConfigureOpenBaoDialogViewModel"/>, just with AWX's fields (job template id
/// instead of mount path/KV version).</summary>
public sealed partial class ConfigureAwxDialogViewModel : ObservableObject
{
	private readonly IIrisApiClient _api;

	public ConfigureAwxDialogViewModel(IIrisApiClient api, string? currentEndpoint)
	{
		_api = api;
		Endpoint = string.IsNullOrWhiteSpace(currentEndpoint) ? string.Empty : currentEndpoint;
	}

	[ObservableProperty] private string _endpoint;
	[ObservableProperty] private string _token = string.Empty;
	[ObservableProperty] private string _jobTemplateId = string.Empty;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _error;

	public bool HasError => !string.IsNullOrEmpty(Error);

	public bool WasSaved { get; private set; }

	public event EventHandler? CloseRequested;

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnIsBusyChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

	[RelayCommand(CanExecute = nameof(CanSave))]
	private async Task SaveAsync()
	{
		if (string.IsNullOrWhiteSpace(Endpoint))
		{
			Error = "Enter the AWX endpoint.";
			return;
		}

		var jobTemplateId = int.TryParse(JobTemplateId, out var parsed) ? parsed : (int?)null;

		IsBusy = true;
		Error = null;

		try
		{
			await _api.SaveAwxIntegrationSettingsAsync(new SaveAwxIntegrationSettingsRequest(
				Endpoint.Trim(),
				string.IsNullOrEmpty(Token) ? null : Token,
				jobTemplateId));
			WasSaved = true;
			CloseRequested?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Error = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool CanSave() => !IsBusy;

	[RelayCommand]
	private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>Backs the "Configure Ansible" dialog — same shape/pattern as
/// <see cref="ConfigureOpenBaoDialogViewModel"/>. No secret field: this describes a
/// playbook/inventory target, not a credentialed API (see <c>SaveAnsibleIntegrationSettingsRequest</c>).</summary>
public sealed partial class ConfigureAnsibleDialogViewModel : ObservableObject
{
	private readonly IIrisApiClient _api;

	public ConfigureAnsibleDialogViewModel(IIrisApiClient api, string? currentEndpoint)
	{
		_api = api;
		Endpoint = string.IsNullOrWhiteSpace(currentEndpoint) ? string.Empty : currentEndpoint;
	}

	[ObservableProperty] private string _endpoint;
	[ObservableProperty] private string _playbook = "iris-deploy-application.yml";
	[ObservableProperty] private string _inventory = string.Empty;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _error;

	public bool HasError => !string.IsNullOrEmpty(Error);

	public bool WasSaved { get; private set; }

	public event EventHandler? CloseRequested;

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnIsBusyChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

	[RelayCommand(CanExecute = nameof(CanSave))]
	private async Task SaveAsync()
	{
		if (string.IsNullOrWhiteSpace(Endpoint))
		{
			Error = "Enter the Ansible endpoint.";
			return;
		}

		if (string.IsNullOrWhiteSpace(Playbook))
		{
			Error = "Enter the playbook name.";
			return;
		}

		IsBusy = true;
		Error = null;

		try
		{
			await _api.SaveAnsibleIntegrationSettingsAsync(new SaveAnsibleIntegrationSettingsRequest(
				Endpoint.Trim(),
				Playbook.Trim(),
				string.IsNullOrWhiteSpace(Inventory) ? null : Inventory.Trim()));
			WasSaved = true;
			CloseRequested?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Error = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool CanSave() => !IsBusy;

	[RelayCommand]
	private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Backs the "Edit SMTP" dialog. Unlike the three integration dialogs above, saving this one
/// takes effect immediately — <c>SmtpEmailSender</c> reads <c>IMailProviderSettingsRepository</c>
/// fresh on every send, there is no startup-only options singleton to restart for (see
/// <c>SaveMailProviderSettingsHandler</c>). Also offers a real "Send test email" action against
/// the fields as currently typed, before saving — same verification <c>CompleteSetupHandler</c>
/// already requires once, at first run.
/// </summary>
public sealed partial class ConfigureMailDialogViewModel : ObservableObject
{
	private readonly IIrisApiClient _api;

	public ConfigureMailDialogViewModel(IIrisApiClient api, MailProviderSettingsResponse? current)
	{
		_api = api;
		SmtpHost = current?.SmtpHost ?? string.Empty;
		SmtpPort = current?.SmtpPort?.ToString() ?? "587";
		SmtpUsername = current?.SmtpUsername ?? string.Empty;
		FromAddress = current?.FromAddress ?? string.Empty;
		FromDisplayName = current?.FromDisplayName ?? string.Empty;
		EnableSsl = current?.EnableSsl ?? true;
	}

	[ObservableProperty] private string _smtpHost;
	[ObservableProperty] private string _smtpPort;
	[ObservableProperty] private string _smtpUsername;
	[ObservableProperty] private string _smtpPassword = string.Empty;
	[ObservableProperty] private string _fromAddress;
	[ObservableProperty] private string _fromDisplayName;
	[ObservableProperty] private bool _enableSsl;
	[ObservableProperty] private string _testRecipient = string.Empty;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _error;
	[ObservableProperty] private string? _info;

	public bool HasError => !string.IsNullOrEmpty(Error);

	public bool HasInfo => !string.IsNullOrEmpty(Info);

	public bool WasSaved { get; private set; }

	public event EventHandler? CloseRequested;

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnInfoChanged(string? value) => OnPropertyChanged(nameof(HasInfo));

	partial void OnIsBusyChanged(bool value)
	{
		SaveCommand.NotifyCanExecuteChanged();
		SendTestEmailCommand.NotifyCanExecuteChanged();
	}

	private bool TryBuildInput(out MailProviderInput input, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? validationError)
	{
		input = null!;
		if (string.IsNullOrWhiteSpace(SmtpHost))
		{
			validationError = "SMTP host is required.";
			return false;
		}

		if (!int.TryParse(SmtpPort, out var port) || port is <= 0 or > 65535)
		{
			validationError = "Enter a valid SMTP port (1-65535).";
			return false;
		}

		if (string.IsNullOrWhiteSpace(FromAddress))
		{
			validationError = "A \"from\" address is required.";
			return false;
		}

		validationError = null;
		input = new MailProviderInput(
			SmtpHost.Trim(),
			port,
			string.IsNullOrWhiteSpace(SmtpUsername) ? null : SmtpUsername.Trim(),
			string.IsNullOrEmpty(SmtpPassword) ? null : SmtpPassword,
			FromAddress.Trim(),
			string.IsNullOrWhiteSpace(FromDisplayName) ? null : FromDisplayName.Trim(),
			EnableSsl);
		return true;
	}

	[RelayCommand(CanExecute = nameof(CanSave))]
	private async Task SaveAsync()
	{
		if (!TryBuildInput(out var input, out var validationError))
		{
			Error = validationError;
			return;
		}

		IsBusy = true;
		Error = null;
		Info = null;

		try
		{
			await _api.SaveMailProviderSettingsAsync(input);
			WasSaved = true;
			CloseRequested?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Error = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool CanSave() => !IsBusy;

	[RelayCommand(CanExecute = nameof(CanSendTestEmail))]
	private async Task SendTestEmailAsync()
	{
		if (!TryBuildInput(out var input, out var validationError))
		{
			Error = validationError;
			return;
		}

		if (string.IsNullOrWhiteSpace(TestRecipient))
		{
			Error = "Enter a recipient address for the test email.";
			return;
		}

		IsBusy = true;
		Error = null;
		Info = null;

		try
		{
			await _api.TestMailSettingsAsync(input, TestRecipient.Trim());
			Info = $"Test email sent to {TestRecipient.Trim()}.";
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Error = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool CanSendTestEmail() => !IsBusy;

	[RelayCommand]
	private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Backs the "Unlock secrets" dialog — a single re-entered password, sent once to
/// <c>POST /system/settings/secrets/unlock</c> and never kept around afterward. See
/// <c>IFallbackSecretVault</c>/<c>EncryptedSecretEntry</c> for what this actually does: persists
/// any secret this process only has in memory (encrypted under this password), and restores any
/// durable one this admin owns that isn't in memory yet. Same safe-dialog pattern as every other
/// dialog here — single <see cref="CloseRequested"/>, page reacts to <see cref="WasSaved"/> only
/// after the window is verifiably closed.
/// </summary>
public sealed partial class UnlockFallbackSecretsDialogViewModel(IIrisApiClient api) : ObservableObject
{
	[ObservableProperty] private string _password = string.Empty;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _error;

	public bool HasError => !string.IsNullOrEmpty(Error);

	/// <summary>True once <see cref="UnlockAsync"/> has succeeded — the page reloads System
	/// settings only then, same as every other "Configure" dialog's <c>WasSaved</c>.</summary>
	public bool WasSaved { get; private set; }

	public string ResultSummary { get; private set; } = string.Empty;

	public event EventHandler? CloseRequested;

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnIsBusyChanged(bool value) => UnlockCommand.NotifyCanExecuteChanged();

	[RelayCommand(CanExecute = nameof(CanUnlock))]
	private async Task UnlockAsync()
	{
		if (string.IsNullOrEmpty(Password))
		{
			Error = "Enter your password.";
			return;
		}

		IsBusy = true;
		Error = null;

		try
		{
			var result = await api.UnlockFallbackSecretsAsync(Password);
			ResultSummary = result.Message;
			WasSaved = true;
			CloseRequested?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Error = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool CanUnlock() => !IsBusy;

	[RelayCommand]
	private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
