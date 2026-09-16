using System.Collections.ObjectModel;
using Iris.Contracts.Applications;

namespace Iris.App.ViewModels;

/// <summary>
/// The Actions module: every <c>PreparedAction</c> across every installation, filterable by
/// customer/application/status, with inline Execute/Cancel for whichever are still <c>Prepared</c>.
/// <see cref="ActionRowViewModel.EffectiveStatus"/> already chains a not-yet-executed action's own
/// status with its linked run's status once executed, so this list reads as one continuous arc
/// (Prepared → Pending → Running → Succeeded/Failed/Canceled) without this page needing to know
/// which of the two entities produced it.
/// </summary>
public partial class ActionsViewModel : ObservableObject
{
	private const string RunPermission = "actions.run";

	private readonly IIrisApiClient _api;
	private readonly IAuthService _auth;
	private bool _loaded;
	private IReadOnlyList<ActionSummaryResponse> _allActions = [];

	public ActionsViewModel(IIrisApiClient api, IAuthService auth)
	{
		_api = api;
		_auth = auth;
	}

	public ObservableCollection<ActionRowViewModel> Actions { get; } = [];

	public ObservableCollection<string> CustomerFilterOptions { get; } = ["All"];
	public ObservableCollection<string> ApplicationFilterOptions { get; } = ["All"];
	public ObservableCollection<string> StatusFilterOptions { get; } = ["All"];

	[ObservableProperty] private string _selectedCustomerFilter = "All";
	[ObservableProperty] private string _selectedApplicationFilter = "All";
	[ObservableProperty] private string _selectedStatusFilter = "All";

	[ObservableProperty] private bool _isLoading;
	[ObservableProperty] private string? _error;

	public bool HasError => !string.IsNullOrEmpty(Error);

	public bool HasActions => Actions.Count > 0;

	public bool CanRunActions => _auth.Me?.EffectivePermissions.Contains(RunPermission) == true;

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnSelectedCustomerFilterChanged(string value) => ApplyFilters();

	partial void OnSelectedApplicationFilterChanged(string value) => ApplyFilters();

	partial void OnSelectedStatusFilterChanged(string value) => ApplyFilters();

	[RelayCommand]
	private async Task LoadAsync()
	{
		if (_loaded)
		{
			return;
		}

		await RefreshAsync();
		_loaded = true;
	}

	[RelayCommand]
	private async Task RefreshAsync()
	{
		IsLoading = true;
		Error = null;

		try
		{
			_allActions = await _api.ListActionsAsync();

			ReplaceOptions(CustomerFilterOptions, SelectedCustomerFilter, _allActions.Select(a => a.CustomerName));
			ReplaceOptions(ApplicationFilterOptions, SelectedApplicationFilter, _allActions.Select(a => a.ApplicationSlug));
			ReplaceOptions(StatusFilterOptions, SelectedStatusFilter, _allActions.Select(a => a.EffectiveStatus));

			ApplyFilters();
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Error = ex.Message;
		}
		finally
		{
			IsLoading = false;
		}
	}

	/// <summary>Rebuilds a filter's option list from the freshly loaded actions, keeping "All" first
	/// and preserving the current selection when it still exists among the new values.</summary>
	private static void ReplaceOptions(ObservableCollection<string> options, string currentSelection, IEnumerable<string> values)
	{
		var distinct = values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray();
		options.Clear();
		options.Add("All");
		foreach (var value in distinct)
		{
			options.Add(value);
		}
	}

	private void ApplyFilters()
	{
		var filtered = _allActions.Where(a =>
				(SelectedCustomerFilter == "All" || string.Equals(a.CustomerName, SelectedCustomerFilter, StringComparison.OrdinalIgnoreCase)) &&
				(SelectedApplicationFilter == "All" || string.Equals(a.ApplicationSlug, SelectedApplicationFilter, StringComparison.OrdinalIgnoreCase)) &&
				(SelectedStatusFilter == "All" || string.Equals(a.EffectiveStatus, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase)))
			.OrderByDescending(a => a.CreatedAtUtc);

		Actions.Clear();
		foreach (var action in filtered)
		{
			Actions.Add(new ActionRowViewModel(action, this));
		}

		OnPropertyChanged(nameof(HasActions));
	}

	internal async Task ExecuteAsync(ActionRowViewModel row)
	{
		row.IsBusy = true;
		row.ActionError = null;
		try
		{
			await _api.ExecutePreparedActionAsync(row.Id);
			await RefreshAsync();
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			row.ActionError = ex.Message;
		}
		finally
		{
			row.IsBusy = false;
		}
	}

	internal async Task CancelAsync(ActionRowViewModel row)
	{
		row.IsBusy = true;
		row.ActionError = null;
		try
		{
			await _api.CancelPreparedActionAsync(row.Id, null);
			await RefreshAsync();
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			row.ActionError = ex.Message;
		}
		finally
		{
			row.IsBusy = false;
		}
	}
}

/// <summary>One row in the Actions list (<c>GET /actions</c>).</summary>
public sealed partial class ActionRowViewModel : ObservableObject
{
	private readonly ActionsViewModel _parent;

	public ActionRowViewModel(ActionSummaryResponse action, ActionsViewModel parent)
	{
		_parent = parent;
		Id = action.Id;
		InstallationName = action.InstallationName;
		ApplicationSlug = action.ApplicationSlug;
		ApplicationVersion = action.ApplicationVersion;
		CustomerName = action.CustomerName;
		Environment = action.Environment;
		ServerName = action.ServerName;
		EffectiveStatus = action.EffectiveStatus;
		CreatedAtUtc = action.CreatedAtUtc;
	}

	public Guid Id { get; }

	public string InstallationName { get; }

	public string ApplicationSlug { get; }

	public string ApplicationVersion { get; }

	public string CustomerName { get; }

	public string Environment { get; }

	public string ServerName { get; }

	public string EffectiveStatus { get; }

	public DateTimeOffset CreatedAtUtc { get; }

	public string CreatedAtText => CreatedAtUtc.LocalDateTime.ToString("g");

	public bool CanRunActions => _parent.CanRunActions;

	public bool IsPrepared => string.Equals(EffectiveStatus, "Prepared", StringComparison.OrdinalIgnoreCase);

	public bool IsSucceeded => string.Equals(EffectiveStatus, "Succeeded", StringComparison.OrdinalIgnoreCase);

	public bool IsFailedOrCanceled => string.Equals(EffectiveStatus, "Failed", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(EffectiveStatus, "Canceled", StringComparison.OrdinalIgnoreCase);

	public bool IsInProgress => !IsPrepared && !IsSucceeded && !IsFailedOrCanceled;

	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _actionError;

	public bool HasActionError => !string.IsNullOrWhiteSpace(ActionError);

	partial void OnActionErrorChanged(string? value) => OnPropertyChanged(nameof(HasActionError));

	[RelayCommand]
	private Task ExecuteAsync() => _parent.ExecuteAsync(this);

	[RelayCommand]
	private Task CancelAsync() => _parent.CancelAsync(this);
}
