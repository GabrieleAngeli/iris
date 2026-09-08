using System.Collections.ObjectModel;

namespace Iris.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
	private readonly IDashboardDataService _data;
	private readonly IAuthService _auth;
	private readonly IIrisApiClient _api;

	public DashboardViewModel(IDashboardDataService data, IAuthService auth, IIrisApiClient api)
	{
		_data = data;
		_auth = auth;
		_api = api;
	}

	public ObservableCollection<StatCard> Stats { get; } = [];
	public ObservableCollection<ActivityItem> Activity { get; } = [];
	public ObservableCollection<ProjectRow> Projects { get; } = [];
	public ObservableCollection<ChartBar> Traffic { get; } = [];

	[ObservableProperty] private string _greeting = "Welcome back";
	[ObservableProperty] private string _userName = "there";
	[ObservableProperty] private string _today = DateTime.Now.ToString("dddd, d MMMM yyyy");
	[ObservableProperty] private bool _isLoading = true;
	[ObservableProperty] private bool _isRefreshing;
	[ObservableProperty] private string? _integrationWarning;

	public bool HasIntegrationWarning => !string.IsNullOrEmpty(IntegrationWarning);

	partial void OnIntegrationWarningChanged(string? value) => OnPropertyChanged(nameof(HasIntegrationWarning));

	[RelayCommand]
	private async Task GoToSystemSettingsAsync() => await Shell.Current.GoToAsync("//system-settings");

	private bool _loaded;

	[RelayCommand]
	private async Task LoadAsync()
	{
		if (_loaded)
			return;

		IsLoading = true;
		await PopulateAsync(1200);
		IsLoading = false;
		_loaded = true;
	}

	[RelayCommand]
	private async Task RefreshAsync()
	{
		IsRefreshing = true;
		await PopulateAsync(700);
		IsRefreshing = false;
	}

	private async Task PopulateAsync(int delayMs)
	{
		await Task.Delay(delayMs);

		UserName = string.IsNullOrWhiteSpace(_auth.CurrentUser) ? "there" : _auth.CurrentUser!;
		Greeting = DateTime.Now.Hour switch
		{
			< 12 => "Good morning",
			< 18 => "Good afternoon",
			_ => "Good evening"
		};

		Replace(Stats, _data.GetStats());
		Replace(Activity, _data.GetRecentActivity());
		Replace(Projects, _data.GetProjects());
		Replace(Traffic, _data.GetWeeklyTraffic());

		await RefreshIntegrationWarningAsync();
	}

	/// <summary>Surfaces a single dashboard-level warning when something under System settings
	/// needs attention — a real signal (not mock data, unlike the rest of this page today),
	/// sourced from the same <c>GET /system/settings</c> the System settings page itself reads.
	/// Requested by the user (2026-09-08): "deve dare una notifica in dashboard" for services
	/// that aren't configured/tested. Deliberately a single summary line, not a full health
	/// panel — the details already live one click away in System settings.</summary>
	private async Task RefreshIntegrationWarningAsync()
	{
		if (_auth.Me?.EffectivePermissions.Contains("platform.admin") != true)
		{
			IntegrationWarning = null;
			return;
		}

		try
		{
			var settings = await _api.GetSystemSettingsAsync();
			var problems = new List<string>();

			if (settings.Mail is null || !settings.Mail.IsConfigured)
			{
				problems.Add("SMTP");
			}

			foreach (var integration in settings.Integrations)
			{
				if (integration.Key is "openbao" or "awx" or "ansible" &&
					integration.Status is not ("Configured" or "Reachable"))
				{
					problems.Add(integration.Name);
				}
			}

			if (settings.RestartRequired)
			{
				problems.Add("a pending restart");
			}

			IntegrationWarning = problems.Count == 0
				? null
				: $"Needs attention: {string.Join(", ", problems)}.";
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			// Best-effort: a dashboard widget failing to load must never block the rest of the
			// page (all still-mock data above renders regardless).
			IntegrationWarning = null;
		}
	}

	[RelayCommand]
	private async Task OpenActivityAsync(ActivityItem? item)
	{
		if (item is null)
			return;

		await Shell.Current.GoToAsync("activitydetail", new Dictionary<string, object>
		{
			["Title"] = item.Title,
			["Description"] = item.Description,
			["Timestamp"] = item.Timestamp,
			["Category"] = item.Category
		});
	}

	private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
	{
		target.Clear();
		foreach (var item in source)
			target.Add(item);
	}
}
