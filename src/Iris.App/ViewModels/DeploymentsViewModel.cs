using System.Collections.ObjectModel;
using Iris.Contracts.Deployments;
using Iris.Contracts.Tenancy;

namespace Iris.App.ViewModels;

/// <summary>
/// Deployments: "this application, for this customer's context, on this server" — composed and
/// browsed by customer, not buried under the application catalog. Composition is top-down: assign
/// the servers an environment uses first (<see cref="EnvironmentServerAssignmentResponse"/>), then
/// per server decide which applications and installation mode run there. Reuses
/// <see cref="ApplicationsViewModel"/> as the source of <see cref="ApplicationRowViewModel"/>s (and
/// its already-working installation draft/wizard) purely as a picker backing collection; this page
/// owns none of that state itself.
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

	/// <summary>The application catalog, used only to back the "New deployment" / "Add application" picker.</summary>
	public ObservableCollection<ApplicationRowViewModel> Applications => _applications.Applications;

	[ObservableProperty] private bool _isLoading;
	[ObservableProperty] private string? _error;

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
			var assignmentsTask = _api.GetEnvironmentServerAssignmentsAsync();
			var serversTask = _api.GetServersAsync();
			var customers = await customersTask;
			var installations = await installationsTask;
			var assignments = await assignmentsTask;
			var servers = (await serversTask).Where(s => s.IsActive).ToArray();

			CustomerGroups.Clear();
			foreach (var customer in customers.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
			{
				var group = new DeploymentCustomerGroupViewModel(customer);
				foreach (var context in customer.Contexts.OrderBy(c => c.Kind, StringComparer.OrdinalIgnoreCase))
				{
					var contextAssignments = assignments
						.Where(a => a.CustomerContextId == context.Id)
						.OrderBy(a => a.ServerName, StringComparer.OrdinalIgnoreCase)
						.ToArray();

					var contextGroup = new DeploymentContextGroupViewModel(context, CanManageDeployments, RequestAssignServerAsync);
					foreach (var assignment in contextAssignments)
					{
						var serverGroup = new DeploymentServerGroupViewModel(
							assignment.Id, assignment.ServerNodeId, assignment.ServerName, context.Id,
							CanManageDeployments, RequestUnassignServer, RequestAddApplication);
						foreach (var installation in installations.Where(i =>
							i.CustomerContextId == context.Id && i.ServerNodeId == assignment.ServerNodeId))
						{
							serverGroup.Installations.Add(new ApplicationInstallationRowViewModel(
								installation, _api, CanManageDeployments, RaiseInstallationOpsRequested));
						}

						contextGroup.Servers.Add(serverGroup);
					}

					var assignedServerIds = contextAssignments.Select(a => a.ServerNodeId).ToHashSet();
					foreach (var server in servers
						.Where(s => !assignedServerIds.Contains(s.Id))
						.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
					{
						contextGroup.AvailableServers.Add(new ServerOptionViewModel(server));
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

	/// <summary>
	/// Raised to ask the view to open a small "pick an application" dialog (there is no
	/// page-level application picker anymore — every entry point into composing a deployment
	/// asks for the application as its first step instead).
	/// </summary>
	public event EventHandler<SelectApplicationDialogViewModel>? SelectApplicationRequested;

	private void RaiseInstallationOpsRequested(ApplicationInstallationRowViewModel row) =>
		InstallationOpsRequested?.Invoke(this, row);

	[RelayCommand]
	private void RequestNewDeployment() => RequestApplicationPicker(null, null);

	/// <summary>
	/// Asks the operator which application to deploy (only those with an imported release are
	/// offered). Only *raises the request* — does not wait for an answer here. The view is
	/// responsible for fully closing the picker window (via <see cref="IDialogService.ShowAsync"/>,
	/// which only returns once the native window is actually closed) before calling
	/// <see cref="HandleApplicationPickedAsync"/>. Do not resolve/await this in-place with a
	/// second, independent completion signal (an earlier version did, via a TaskCompletionSource
	/// racing the window's own close) — that let the next dialog start opening while this one was
	/// still closing and crashed the app.
	/// </summary>
	private void RequestApplicationPicker(Guid? presetServerId, Guid? presetContextId)
	{
		var deployable = Applications.Where(a => a.CanRequestNewInstallation).ToArray();
		if (deployable.Length == 0)
		{
			Error = "No application has an imported release yet — import configuration knowledge on the Applications page first.";
			return;
		}

		var picker = new SelectApplicationDialogViewModel(deployable, presetServerId, presetContextId);
		SelectApplicationRequested?.Invoke(this, picker);
	}

	/// <summary>
	/// Called by the view strictly after the picker window has fully closed
	/// (<c>await IDialogService.ShowAsync(...)</c> returned) — never from inside the window's own
	/// Confirm/Cancel handling. Only then is it safe to open the next window.
	/// </summary>
	public async Task HandleApplicationPickedAsync(SelectApplicationDialogViewModel picker)
	{
		if (!picker.WasConfirmed || picker.SelectedApplication is not { } application)
		{
			return;
		}

		await StartComposingAsync(application, picker.PresetServerId, picker.PresetContextId);
	}

	/// <summary>
	/// Opens the existing installation wizard for <paramref name="application"/>, then — once it
	/// has finished loading its server/context options — pre-selects <paramref name="presetServerId"/>
	/// and <paramref name="presetContextId"/> when given. The wizard still lets the operator change
	/// them; this only saves the obvious re-selection when composing from a server row.
	/// </summary>
	private async Task StartComposingAsync(ApplicationRowViewModel application, Guid? presetServerId, Guid? presetContextId)
	{
		await application.RequestNewInstallationCommand.ExecuteAsync(null);

		if (presetServerId is { } serverId)
		{
			var server = application.InstallServerOptions.FirstOrDefault(option => option.Id == serverId);
			if (server is not null)
			{
				application.SelectedInstallServer = server;
			}
		}

		if (presetContextId is { } contextId)
		{
			var context = application.InstallCustomerContextOptions.FirstOrDefault(option => option.ContextId == contextId);
			if (context is not null)
			{
				application.SelectedInstallCustomerContext = context;
			}
		}
	}

	private void RequestAddApplication(DeploymentServerGroupViewModel serverGroup) =>
		RequestApplicationPicker(serverGroup.ServerNodeId, serverGroup.CustomerContextId);

	private async Task RequestAssignServerAsync(DeploymentContextGroupViewModel contextGroup)
	{
		if (contextGroup.SelectedServerToAssign is not { } server)
		{
			contextGroup.AssignServerError = "Select a server to assign.";
			return;
		}

		try
		{
			await _api.AssignServerToEnvironmentAsync(contextGroup.Id, new AssignServerToEnvironmentRequest(server.Id));
			await RefreshAsync();
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			contextGroup.AssignServerError = ex.Message;
		}
	}

	private async void RequestUnassignServer(DeploymentServerGroupViewModel serverGroup)
	{
		try
		{
			await _api.UnassignServerFromEnvironmentAsync(serverGroup.AssignmentId);
			await RefreshAsync();
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			Error = ex.Message;
		}
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

/// <summary>
/// One customer context (environment). Deployments into it are shown one level deeper, by
/// server: assign the servers that host this environment first (<see cref="AssignServerCommand"/>),
/// then per server the applications and their installation mode — not a flat list of installations.
/// </summary>
public sealed partial class DeploymentContextGroupViewModel : ObservableObject
{
	private readonly Func<DeploymentContextGroupViewModel, Task> _assignServer;

	public DeploymentContextGroupViewModel(
		ContextSummaryResponse context,
		bool canManageDeployments,
		Func<DeploymentContextGroupViewModel, Task> assignServer)
	{
		Id = context.Id;
		Name = context.Name;
		Kind = context.Kind;
		IsActive = context.IsActive;
		CanManageDeployments = canManageDeployments;
		_assignServer = assignServer;
	}

	public Guid Id { get; }

	public string Name { get; }

	public string Kind { get; }

	public bool IsActive { get; }

	public bool CanManageDeployments { get; }

	public ObservableCollection<DeploymentServerGroupViewModel> Servers { get; } = [];

	public bool HasServers => Servers.Count > 0;

	/// <summary>Active servers not yet assigned to this environment — the "assign a server" picker.</summary>
	public ObservableCollection<ServerOptionViewModel> AvailableServers { get; } = [];

	public bool HasAvailableServers => AvailableServers.Count > 0;

	[ObservableProperty] private ServerOptionViewModel? _selectedServerToAssign;
	[ObservableProperty] private bool _isAssigningServer;
	[ObservableProperty] private string? _assignServerError;

	public bool HasAssignServerError => !string.IsNullOrWhiteSpace(AssignServerError);

	partial void OnAssignServerErrorChanged(string? value) => OnPropertyChanged(nameof(HasAssignServerError));

	[RelayCommand]
	private async Task AssignServer()
	{
		IsAssigningServer = true;
		AssignServerError = null;

		try
		{
			await _assignServer(this);
		}
		finally
		{
			IsAssigningServer = false;
		}
	}
}

/// <summary>
/// One server assigned to host an environment, and the applications (with installation mode)
/// installed on it there.
/// </summary>
public sealed partial class DeploymentServerGroupViewModel : ObservableObject
{
	private readonly Action<DeploymentServerGroupViewModel> _unassign;
	private readonly Action<DeploymentServerGroupViewModel> _addApplication;

	public DeploymentServerGroupViewModel(
		Guid assignmentId,
		Guid serverNodeId,
		string serverName,
		Guid customerContextId,
		bool canManageDeployments,
		Action<DeploymentServerGroupViewModel> unassign,
		Action<DeploymentServerGroupViewModel> addApplication)
	{
		AssignmentId = assignmentId;
		ServerNodeId = serverNodeId;
		ServerName = serverName;
		CustomerContextId = customerContextId;
		CanManageDeployments = canManageDeployments;
		_unassign = unassign;
		_addApplication = addApplication;
	}

	public Guid AssignmentId { get; }

	public Guid ServerNodeId { get; }

	public string ServerName { get; }

	public Guid CustomerContextId { get; }

	public bool CanManageDeployments { get; }

	public ObservableCollection<ApplicationInstallationRowViewModel> Installations { get; } = [];

	public bool HasInstallations => Installations.Count > 0;

	[RelayCommand]
	private void Unassign() => _unassign(this);

	[RelayCommand]
	private void AddApplication() => _addApplication(this);
}

/// <summary>
/// Backs the small "which application?" dialog shown before composing a new deployment —
/// the only place that choice is made now that the page itself has no application picker.
/// Only applications with an imported release (<see cref="ApplicationRowViewModel.CanRequestNewInstallation"/>)
/// are offered; the caller is expected to have filtered the list before constructing this.
///
/// Deliberately exposes a single <see cref="CloseRequested"/> signal (not separate
/// Confirmed/Cancelled events carrying the "what happens next" logic): the view's job on
/// that signal is only to close its window, nothing else. Whatever should happen after a
/// confirmed pick (opening the next wizard) must wait until the window is verifiably closed
/// — read <see cref="WasConfirmed"/>/<see cref="SelectedApplication"/> only after
/// <c>await IDialogService.ShowAsync(...)</c> for this dialog has returned
/// (<see cref="DeploymentsViewModel.HandleApplicationPickedAsync"/>). An earlier version
/// resolved a separate TaskCompletionSource on the same Confirmed event that also closed the
/// window, which let the next window start opening before this one had finished closing and
/// crashed the app — do not reintroduce that shape.
/// </summary>
public sealed partial class SelectApplicationDialogViewModel : ObservableObject
{
	public SelectApplicationDialogViewModel(IReadOnlyList<ApplicationRowViewModel> applications, Guid? presetServerId, Guid? presetContextId)
	{
		Applications = applications;
		SelectedApplication = applications.FirstOrDefault();
		PresetServerId = presetServerId;
		PresetContextId = presetContextId;
	}

	public IReadOnlyList<ApplicationRowViewModel> Applications { get; }

	public Guid? PresetServerId { get; }

	public Guid? PresetContextId { get; }

	/// <summary>Set only by <see cref="Confirm"/>. False for Cancel and for the window being
	/// closed any other way (native close button), which is the correct "do nothing further"
	/// outcome in both those cases.</summary>
	public bool WasConfirmed { get; private set; }

	[ObservableProperty] private ApplicationRowViewModel? _selectedApplication;
	[ObservableProperty] private string? _error;

	public bool HasError => !string.IsNullOrWhiteSpace(Error);

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	/// <summary>The view's only reaction to this must be closing its window — see the class
	/// remarks for why nothing else may hang off this event.</summary>
	public event EventHandler? CloseRequested;

	[RelayCommand]
	private void Confirm()
	{
		if (SelectedApplication is null)
		{
			Error = "Select an application to continue.";
			return;
		}

		WasConfirmed = true;
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}

	[RelayCommand]
	private void Cancel()
	{
		WasConfirmed = false;
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}
}
