using System.Collections.ObjectModel;
using Iris.Contracts.Audit;
using Iris.Contracts.Settings;

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

	[RelayCommand(CanExecute = nameof(CanLoad))]
	private async Task LoadAsync()
	{
		IsBusy = true;
		Error = null;

		try
		{
			var settings = await api.GetSystemSettingsAsync();
			CanManageSystem = settings.CanManageSystem;
			ApplyMail(settings.Mail);

			Integrations.Clear();
			foreach (var integration in settings.Integrations)
			{
				Integrations.Add(new IntegrationConnectionRow(integration, TestIntegrationAsync));
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

	[ObservableProperty] private string _key;
	[ObservableProperty] private string _name;
	[ObservableProperty] private string _status;
	[ObservableProperty] private string _endpoint;
	[ObservableProperty] private string _message;
	[ObservableProperty] private bool _isBusy;

	public IntegrationConnectionRow(
		IntegrationLinkResponse response,
		Func<IntegrationConnectionRow, Task<IntegrationLinkResponse>> tester)
	{
		_tester = tester;
		_key = response.Key;
		_name = response.Name;
		_status = response.Status;
		_endpoint = string.IsNullOrWhiteSpace(response.Endpoint) ? "-" : response.Endpoint;
		_message = response.Message ?? string.Empty;
	}

	public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

	partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

	partial void OnIsBusyChanged(bool value) => TestCommand.NotifyCanExecuteChanged();

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

	private bool CanTest() => !IsBusy;

	private void Apply(IntegrationLinkResponse response)
	{
		Name = response.Name;
		Status = response.Status;
		Endpoint = string.IsNullOrWhiteSpace(response.Endpoint) ? "-" : response.Endpoint;
		Message = response.Message ?? string.Empty;
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
