using System.Collections.ObjectModel;
using Iris.Contracts.Tenancy;

namespace Iris.App.ViewModels;

/// <summary>
/// Deployments: "this application, for this customer's context, on this server" — composed and
/// browsed by customer, not buried under the application catalog. Reuses <see cref="ApplicationsViewModel"/>
/// as the source of <see cref="ApplicationRowViewModel"/>s (and its already-working installation
/// draft/wizard) purely as a picker backing collection; this page owns none of that state itself.
/// </summary>
public partial class DeploymentsViewModel : ObservableObject
{
	private const string ReadPermission = "deployments.read";
	private const string WritePermission = "deployments.write";

	private readonly IIrisApiClient _api;
	private readonly IAuthService _auth;
	private readonly ApplicationsViewModel _applications;
	private readonly EventHandler _onInstallationCompleted;

	public DeploymentsViewModel(IIrisApiClient api, IAuthService auth, ApplicationsViewModel applications)
	{
		_api = api;
		_auth = auth;
		_applications = applications;
		_onInstallationCompleted = async (_, _) => await RefreshAsync();
		_applications.NewApplicationInstallationRequested += (_, row) => NewApplicationInstallationRequested?.Invoke(this, row);
	}

	public ObservableCollection<DeploymentCustomerGroupViewModel> CustomerGroups { get; } = [];

	/// <summary>The application catalog, used only to back the "New deployment" application picker.</summary>
	public ObservableCollection<ApplicationRowViewModel> Applications => _applications.Applications;

	[ObservableProperty] private bool _isLoading;
	[ObservableProperty] private string? _error;
	[ObservableProperty] private ApplicationRowViewModel? _selectedNewDeploymentApplication;

	public bool HasError => !string.IsNullOrEmpty(Error);

	public bool CanSeeDeployments => _auth.Me?.EffectivePermissions.Contains(ReadPermission) == true;

	public bool CanManageDeployments => _auth.Me?.EffectivePermissions.Contains(WritePermission) == true;

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	private bool _loaded;

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
			await _applications.LoadCommand.ExecuteAsync(null);
			foreach (var row in _applications.Applications)
			{
				row.ApplicationInstallationCompleted -= _onInstallationCompleted;
				row.ApplicationInstallationCompleted += _onInstallationCompleted;
			}

			var customersTask = _api.GetCustomersAsync();
			var installationsTask = _api.GetApplicationInstallationsAsync();
			var customers = await customersTask;
			var installations = await installationsTask;

			CustomerGroups.Clear();
			foreach (var customer in customers.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
			{
				var group = new DeploymentCustomerGroupViewModel(customer);
				foreach (var context in customer.Contexts.OrderBy(c => c.Kind, StringComparer.OrdinalIgnoreCase))
				{
					var contextGroup = new DeploymentContextGroupViewModel(context);
					foreach (var installation in installations.Where(i => i.CustomerContextId == context.Id))
					{
						contextGroup.Installations.Add(new ApplicationInstallationRowViewModel(
							installation, _api, CanManageDeployments, RaiseInstallationOpsRequested));
					}

					group.Contexts.Add(contextGroup);
				}

				CustomerGroups.Add(group);
			}
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

	public event EventHandler<ApplicationRowViewModel>? NewApplicationInstallationRequested;

	public event EventHandler<ApplicationInstallationRowViewModel>? InstallationOpsRequested;

	private void RaiseInstallationOpsRequested(ApplicationInstallationRowViewModel row) =>
		InstallationOpsRequested?.Invoke(this, row);

	[RelayCommand]
	private void RequestNewDeployment()
	{
		if (SelectedNewDeploymentApplication is null)
		{
			Error = "Select an application to deploy first.";
			return;
		}

		if (SelectedNewDeploymentApplication.RequestNewInstallationCommand.CanExecute(null))
		{
			SelectedNewDeploymentApplication.RequestNewInstallationCommand.Execute(null);
			return;
		}

		Error = $"'{SelectedNewDeploymentApplication.Name}' has no imported release yet — import its configuration knowledge on the Applications page first.";
	}
}

/// <summary>One customer, grouping the contexts (environments) a deployment can target.</summary>
public sealed class DeploymentCustomerGroupViewModel(CustomerSummaryResponse customer)
{
	public Guid Id { get; } = customer.Id;

	public string Name { get; } = customer.Name;

	public bool IsActive { get; } = customer.IsActive;

	public ObservableCollection<DeploymentContextGroupViewModel> Contexts { get; } = [];
}

/// <summary>One customer context (environment) and the installations deployed into it.</summary>
public sealed class DeploymentContextGroupViewModel(ContextSummaryResponse context)
{
	public Guid Id { get; } = context.Id;

	public string Name { get; } = context.Name;

	public string Kind { get; } = context.Kind;

	public bool IsActive { get; } = context.IsActive;

	public ObservableCollection<ApplicationInstallationRowViewModel> Installations { get; } = [];

	public bool HasInstallations => Installations.Count > 0;
}
