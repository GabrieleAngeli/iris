using System.Collections.ObjectModel;

namespace Iris.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
	private readonly IDashboardDataService _data;
	private readonly IAuthService _auth;

	public DashboardViewModel(IDashboardDataService data, IAuthService auth)
	{
		_data = data;
		_auth = auth;
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
