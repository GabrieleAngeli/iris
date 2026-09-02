using System.Collections.ObjectModel;
using Iris.Contracts.Infrastructure;

namespace Iris.App.ViewModels;

/// <summary>Infrastructure › Servers: registered servers and the OS-login credentials each holds.</summary>
public partial class ServersViewModel : ObservableObject
{
	private const string SecretsManagePermission = "infrastructure.secrets.manage";

	private readonly IIrisApiClient _api;
	private readonly IAuthService _auth;

	public ServersViewModel(IIrisApiClient api, IAuthService auth)
	{
		_api = api;
		_auth = auth;
		NewServerCredential = NewCredentialForm(isEdit: false);
	}

	/// <summary>True when the signed-in user may rotate credential secrets (Global <c>infrastructure.secrets.manage</c>).</summary>
	public bool CanManageSecrets =>
		_auth.Me?.EffectivePermissions.Contains(SecretsManagePermission) == true;

	internal CredentialFormViewModel NewCredentialForm(bool isEdit) =>
		new(OwnerOptions, isEdit, CanManageSecrets);

	public ObservableCollection<ServerRowViewModel> Servers { get; } = [];

	public ObservableCollection<DataServiceRowViewModel> DataServices { get; } = [];

	public ObservableCollection<InfrastructureResourceRowViewModel> Resources { get; } = [];

	/// <summary>Iris users offered as the "owner" of a system-user credential. Shared with every credential form.</summary>
	public ObservableCollection<UserOption> OwnerOptions { get; } = [UserOption.None];

	[ObservableProperty] private bool _isLoading;
	[ObservableProperty] private string? _error;
	[ObservableProperty] private string _resourceTypeFilter = "All";
	[ObservableProperty] private string _resourceOsFilter = "All";
	[ObservableProperty] private string _resourceVersionFilter = string.Empty;
	[ObservableProperty] private string _resourceTagFilter = string.Empty;
	[ObservableProperty] private string _resourceSortBy = "Name";
	[ObservableProperty] private bool _resourceSortDescending;

	public bool HasError => !string.IsNullOrEmpty(Error);

	public IReadOnlyList<string> ResourceTypeFilterOptions { get; } = ["All", "Server node", "Managed data service"];

	public IReadOnlyList<string> ResourceOsFilterOptions { get; } = ["All", "Linux", "Windows", "N/A"];

	public IReadOnlyList<string> ResourceSortOptions { get; } = ["Name", "Type", "OS", "Version", "Tag"];

	public bool HasResources => Resources.Count > 0;

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnResourceTypeFilterChanged(string value) => RebuildResources();

	partial void OnResourceOsFilterChanged(string value) => RebuildResources();

	partial void OnResourceVersionFilterChanged(string value) => RebuildResources();

	partial void OnResourceTagFilterChanged(string value) => RebuildResources();

	partial void OnResourceSortByChanged(string value) => RebuildResources();

	partial void OnResourceSortDescendingChanged(bool value) => RebuildResources();

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
			await LoadOwnerOptionsAsync();

			var servers = await _api.GetServersAsync();
			var dataServices = await _api.GetDataServicesAsync();
			Servers.Clear();
			foreach (var server in servers)
			{
				Servers.Add(new ServerRowViewModel(server, _api, OwnerOptions, this));
			}

			DataServices.Clear();
			foreach (var dataService in dataServices)
			{
				DataServices.Add(new DataServiceRowViewModel(dataService, _api, this));
			}

			RebuildResources();
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

	private async Task LoadOwnerOptionsAsync()
	{
		try
		{
			var users = await _api.GetUsersAsync();
			OwnerOptions.Clear();
			OwnerOptions.Add(UserOption.None);
			foreach (var user in users.OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase))
			{
				OwnerOptions.Add(new UserOption(user.Id, $"{user.DisplayName} ({user.Email})"));
			}
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			// governance.read is a separate permission from infrastructure.read — if the
			// caller can't list users, a system-user credential just can't be linked to one.
			OwnerOptions.Clear();
			OwnerOptions.Add(UserOption.None);
		}
	}

	// ----- New server -----

	public IReadOnlyList<string> OsOptions { get; } = ["Linux", "Windows"];

	public IReadOnlyList<string> HostingTypeOptions { get; } = ["SelfHosted", "Cloud"];

	public IReadOnlyList<string> EnvironmentOptions { get; } = ["Test", "Staging", "Production"];

	public IReadOnlyList<string> DataServiceKindOptions { get; } = ["Mssql", "PostgreSql", "Redis"];

	public IReadOnlyList<string> NewResourceKindOptions { get; } = ["Server node", "Managed data service"];

	/// <summary>Raised when the operator asks to register a server — the page opens the dialog window.</summary>
	public event EventHandler? NewServerRequested;

	/// <summary>Raised after a server is registered so its dialog window can close.</summary>
	public event EventHandler? NewServerCompleted;

	public event EventHandler? NewDataServiceCompleted;

	/// <summary>Raised (post-create, or from a row's button) to open the add-credential dialog for a server.</summary>
	public event EventHandler<ServerRowViewModel>? AddCredentialRequested;

	/// <summary>Raised from a row's edit button to open the edit/delete dialog for a server.</summary>
	public event EventHandler<ServerRowViewModel>? EditServerRequested;

	/// <summary>Raised from the edit dialog's "Delete this server" button to open the confirm-delete window.</summary>
	public event EventHandler<ServerRowViewModel>? DeleteServerRequested;

	public void RaiseEditRequested(ServerRowViewModel row) => EditServerRequested?.Invoke(this, row);

	/// <summary>
	/// Opens the confirm-delete window for <paramref name="row"/>, on the next UI tick so the edit
	/// dialog it was launched from finishes closing first.
	/// </summary>
	public void RaiseDeleteRequested(ServerRowViewModel row) =>
		MainThread.BeginInvokeOnMainThread(() => DeleteServerRequested?.Invoke(this, row));

	public void RemoveRow(ServerRowViewModel row)
	{
		Servers.Remove(row);
		RebuildResources();
	}

	internal void RebuildResources()
	{
		var rows = Servers
			.Select(server => new InfrastructureResourceRowViewModel(server))
			.Concat(DataServices.Select(dataService => new InfrastructureResourceRowViewModel(dataService)));

		if (ResourceTypeFilter != "All")
		{
			rows = rows.Where(row => row.Type == ResourceTypeFilter);
		}

		if (ResourceOsFilter != "All")
		{
			rows = ResourceOsFilter == "N/A"
				? rows.Where(row => string.IsNullOrWhiteSpace(row.Os))
				: rows.Where(row => string.Equals(row.Os, ResourceOsFilter, StringComparison.OrdinalIgnoreCase));
		}

		if (!string.IsNullOrWhiteSpace(ResourceVersionFilter))
		{
			rows = rows.Where(row => row.Version.Contains(ResourceVersionFilter.Trim(), StringComparison.OrdinalIgnoreCase));
		}

		if (!string.IsNullOrWhiteSpace(ResourceTagFilter))
		{
			rows = rows.Where(row => row.TagText.Contains(ResourceTagFilter.Trim(), StringComparison.OrdinalIgnoreCase));
		}

		rows = ResourceSortBy switch
		{
			"Type" => rows.OrderBy(row => row.Type, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
			"OS" => rows.OrderBy(row => row.Os, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
			"Version" => rows.OrderBy(row => row.Version, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
			"Tag" => rows.OrderBy(row => row.TagText, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
			_ => rows.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase),
		};

		var ordered = rows.ToList();
		if (ResourceSortDescending)
		{
			ordered.Reverse();
		}

		Resources.Clear();
		foreach (var row in ordered)
		{
			Resources.Add(row);
		}

		OnPropertyChanged(nameof(HasResources));
	}

	[ObservableProperty] private string _newServerName = string.Empty;
	[ObservableProperty] private string _newResourceKind = "Server node";
	[ObservableProperty] private string _newServerHostname = string.Empty;
	[ObservableProperty] private string _newServerOs = "Linux";
	[ObservableProperty] private string _newServerHostingType = "SelfHosted";
	[ObservableProperty] private string _newServerEnvironment = "Test";
	[ObservableProperty] private string _newServerPublicIp = string.Empty;
	[ObservableProperty] private string _newServerPrivateIp = string.Empty;
	[ObservableProperty] private bool _includeCredential = true;
	[ObservableProperty] private bool _isCreatingServer;
	[ObservableProperty] private string? _createServerError;
	[ObservableProperty] private string _newDataServiceName = string.Empty;
	[ObservableProperty] private string _newDataServiceKind = "Mssql";
	[ObservableProperty] private string _newDataServiceEndpoint = string.Empty;
	[ObservableProperty] private string _newDataServicePort = string.Empty;
	[ObservableProperty] private string _newDataServiceVersion = string.Empty;
	[ObservableProperty] private string _newDataServiceSize = string.Empty;
	[ObservableProperty] private string _newDataServiceStorageGb = string.Empty;
	[ObservableProperty] private string _newDataServiceEnvironment = "Test";
	[ObservableProperty] private string _newDataServiceUsername = string.Empty;
	[ObservableProperty] private string _newDataServicePassword = string.Empty;
	[ObservableProperty] private bool _isCreatingDataService;
	[ObservableProperty] private string? _createDataServiceError;

	public CredentialFormViewModel NewServerCredential { get; }

	public bool HasCreateServerError => !string.IsNullOrEmpty(CreateServerError);

	public bool HasCreateDataServiceError => !string.IsNullOrEmpty(CreateDataServiceError);

	public bool IsNewServerResource => NewResourceKind == "Server node";

	public bool IsNewDataServiceResource => NewResourceKind == "Managed data service";

	public bool ShowNewServerCredentialSection => IsNewServerResource && IncludeCredential;

	public bool IsCreatingResource => IsCreatingServer || IsCreatingDataService;

	public string CreateResourceError => IsNewDataServiceResource
		? CreateDataServiceError ?? string.Empty
		: CreateServerError ?? string.Empty;

	public bool HasCreateResourceError => !string.IsNullOrEmpty(CreateResourceError);

	partial void OnCreateServerErrorChanged(string? value)
	{
		OnPropertyChanged(nameof(HasCreateServerError));
		OnPropertyChanged(nameof(CreateResourceError));
		OnPropertyChanged(nameof(HasCreateResourceError));
	}

	partial void OnCreateDataServiceErrorChanged(string? value)
	{
		OnPropertyChanged(nameof(HasCreateDataServiceError));
		OnPropertyChanged(nameof(CreateResourceError));
		OnPropertyChanged(nameof(HasCreateResourceError));
	}

	partial void OnNewResourceKindChanged(string value)
	{
		CreateServerError = null;
		CreateDataServiceError = null;
		OnPropertyChanged(nameof(IsNewServerResource));
		OnPropertyChanged(nameof(IsNewDataServiceResource));
		OnPropertyChanged(nameof(ShowNewServerCredentialSection));
		OnPropertyChanged(nameof(CreateResourceError));
		OnPropertyChanged(nameof(HasCreateResourceError));
	}

	partial void OnIncludeCredentialChanged(bool value) => OnPropertyChanged(nameof(ShowNewServerCredentialSection));

	partial void OnIsCreatingServerChanged(bool value) => OnPropertyChanged(nameof(IsCreatingResource));

	partial void OnIsCreatingDataServiceChanged(bool value) => OnPropertyChanged(nameof(IsCreatingResource));

	partial void OnNewDataServiceKindChanged(string value)
	{
		if (string.IsNullOrWhiteSpace(NewDataServicePort) || IsKnownDataServicePort(NewDataServicePort))
		{
			NewDataServicePort = DefaultDataServicePort(value);
		}
	}

	[RelayCommand]
	private void RequestNewServer()
	{
		NewServerName = string.Empty;
		NewResourceKind = "Server node";
		NewServerHostname = string.Empty;
		NewServerPublicIp = string.Empty;
		NewServerPrivateIp = string.Empty;
		NewServerOs = "Linux";
		NewServerHostingType = "SelfHosted";
		NewServerEnvironment = "Test";
		IncludeCredential = true;
		CreateServerError = null;
		ResetNewDataService();
		NewServerCredential.Reset();
		NewServerRequested?.Invoke(this, EventArgs.Empty);
	}

	private void ResetNewDataService()
	{
		NewDataServiceName = string.Empty;
		NewDataServiceKind = "Mssql";
		NewDataServiceEndpoint = string.Empty;
		NewDataServicePort = DefaultDataServicePort(NewDataServiceKind);
		NewDataServiceVersion = string.Empty;
		NewDataServiceSize = string.Empty;
		NewDataServiceStorageGb = string.Empty;
		NewDataServiceEnvironment = "Test";
		NewDataServiceUsername = string.Empty;
		NewDataServicePassword = string.Empty;
		CreateDataServiceError = null;
	}

	internal static string DefaultDataServicePort(string kind) => kind switch
	{
		"PostgreSql" => "5432",
		"Redis" => "6379",
		_ => "1433",
	};

	private static bool IsKnownDataServicePort(string port) => port is "1433" or "5432" or "6379";

	[RelayCommand]
	private async Task CreateSelectedResourceAsync()
	{
		if (IsNewDataServiceResource)
		{
			await CreateDataServiceAsync();
			return;
		}

		await CreateServerAsync();
	}

	/// <summary>Opens the add-credential dialog for <paramref name="row"/> (from its button or right after a bare create).</summary>
	public void OpenCredentialPanel(ServerRowViewModel row)
	{
		row.AddCredentialForm.Reset();
		row.CredentialError = null;
		AddCredentialRequested?.Invoke(this, row);
	}

	[RelayCommand]
	private async Task CreateServerAsync()
	{
		var name = NewServerName.Trim();
		if (name.Length == 0)
		{
			CreateServerError = "Server name is required.";
			return;
		}

		var publicIp = NewServerPublicIp.Trim();
		var privateIp = NewServerPrivateIp.Trim();
		if (publicIp.Length == 0 && privateIp.Length == 0)
		{
			CreateServerError = "Enter at least a public or a private IP address.";
			return;
		}

		ServerCredentialInputRequest? credential = null;
		if (IncludeCredential)
		{
			if (!NewServerCredential.TryBuild(out credential, out var credentialError))
			{
				CreateServerError = credentialError;
				return;
			}
		}

		IsCreatingServer = true;
		CreateServerError = null;

		try
		{
			var request = new CreateServerRequest(
				name,
				string.IsNullOrWhiteSpace(NewServerHostname) ? null : NewServerHostname.Trim(),
				NewServerOs,
				NewServerHostingType,
				publicIp.Length == 0 ? null : publicIp,
				privateIp.Length == 0 ? null : privateIp,
				NewServerEnvironment,
				credential);

			var created = await _api.CreateServerAsync(request);

			var row = new ServerRowViewModel(created, _api, OwnerOptions, this);
			Servers.Insert(0, row);
			RebuildResources();

			NewServerCompleted?.Invoke(this, EventArgs.Empty);

			// A server without a way in isn't useful — chain into the add-credential dialog, but on
			// the next UI tick so the new-server window finishes closing before the next one opens.
			if (created.Credentials.Count == 0)
			{
				var created_row = row;
				MainThread.BeginInvokeOnMainThread(() => OpenCredentialPanel(created_row));
			}
			else
			{
				await row.DiscoverInventoryAsync();
			}
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			CreateServerError = ex.Message;
		}
		finally
		{
			IsCreatingServer = false;
		}
	}

	[RelayCommand]
	private async Task CreateDataServiceAsync()
	{
		var name = NewDataServiceName.Trim();
		var endpoint = NewDataServiceEndpoint.Trim();
		var username = NewDataServiceUsername.Trim();
		if (name.Length == 0 || endpoint.Length == 0 || username.Length == 0 || string.IsNullOrEmpty(NewDataServicePassword))
		{
			CreateDataServiceError = "Name, endpoint, username and password are required.";
			OnPropertyChanged(nameof(CreateResourceError));
			OnPropertyChanged(nameof(HasCreateResourceError));
			return;
		}

		if (!TryParseOptionalInt(NewDataServicePort, "Port", allowZero: false, max: 65535, out var port, out var parseError) ||
			!TryParseOptionalInt(NewDataServiceStorageGb, "Storage GB", allowZero: true, max: null, out var storageGb, out parseError))
		{
			CreateDataServiceError = parseError;
			OnPropertyChanged(nameof(CreateResourceError));
			OnPropertyChanged(nameof(HasCreateResourceError));
			return;
		}

		IsCreatingDataService = true;
		CreateDataServiceError = null;

		try
		{
			var created = await _api.CreateDataServiceAsync(new UpsertDataServiceRequest(
				name,
				NewDataServiceKind,
				endpoint,
				port,
				string.IsNullOrWhiteSpace(NewDataServiceVersion) ? null : NewDataServiceVersion.Trim(),
				string.IsNullOrWhiteSpace(NewDataServiceSize) ? null : NewDataServiceSize.Trim(),
				storageGb,
				NewDataServiceEnvironment,
				true,
				username,
				NewDataServicePassword));

			DataServices.Insert(0, new DataServiceRowViewModel(created, _api, this));
			RebuildResources();
			ResetNewDataService();
			NewDataServiceCompleted?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			CreateDataServiceError = ex.Message;
			OnPropertyChanged(nameof(CreateResourceError));
			OnPropertyChanged(nameof(HasCreateResourceError));
		}
		finally
		{
			IsCreatingDataService = false;
		}
	}

	internal static bool TryParseOptionalInt(
		string value,
		string label,
		bool allowZero,
		int? max,
		out int? result,
		out string? error)
	{
		result = null;
		error = null;

		var trimmed = value.Trim();
		if (trimmed.Length == 0)
		{
			return true;
		}

		if (!int.TryParse(trimmed, out var parsed))
		{
			error = $"{label} must be a whole number.";
			return false;
		}

		if (parsed < 0 || (!allowZero && parsed == 0))
		{
			error = allowZero ? $"{label} cannot be negative." : $"{label} must be greater than zero.";
			return false;
		}

		if (max is { } limit && parsed > limit)
		{
			error = $"{label} cannot be greater than {limit}.";
			return false;
		}

		result = parsed;
		return true;
	}
}

public sealed class InfrastructureResourceRowViewModel
{
	private readonly ServerRowViewModel? _server;
	private readonly DataServiceRowViewModel? _dataService;

	public InfrastructureResourceRowViewModel(ServerRowViewModel server)
	{
		_server = server;
	}

	public InfrastructureResourceRowViewModel(DataServiceRowViewModel dataService)
	{
		_dataService = dataService;
	}

	public ServerRowViewModel? Server => _server;

	public DataServiceRowViewModel? DataService => _dataService;

	public bool IsServer => _server is not null;

	public bool IsDataService => _dataService is not null;

	public string IconGlyph => IsServer ? "\uE7F4" : "\uEFC7";

	public string Type => IsServer ? "Server node" : "Managed data service";

	public string Name => _server?.Name ?? _dataService?.Name ?? string.Empty;

	public string Tech => _server?.Os ?? _dataService?.Kind ?? string.Empty;

	public string Os => _server?.Os ?? string.Empty;

	public string Version => _server?.OsVersion ?? _dataService?.Version ?? string.Empty;

	public bool HasVersion => !string.IsNullOrWhiteSpace(Version);

	public string Environment => _server?.Environment ?? _dataService?.Environment ?? string.Empty;

	public bool IsActive => _server?.IsActive ?? _dataService?.IsActive ?? true;

	public string Endpoint => _server is not null
		? _server.Hostname ?? _server.PrivateIpAddress ?? _server.PublicIpAddress ?? "(no endpoint)"
		: _dataService?.EndpointSummary ?? string.Empty;

	public string ResourceSummary => _server is not null
		? (_server.HasResourceSummary ? _server.ResourceSummary : "No known resources")
		: _dataService?.DetailsSummary ?? string.Empty;

	public string NetworkSummary => _server is not null
		? $"Public: {_server.PublicIpAddress ?? "-"}   Private: {_server.PrivateIpAddress ?? "-"}"
		: _dataService?.CredentialSummary ?? string.Empty;

	public string TagText
	{
		get
		{
			if (_server is not null)
			{
				var tags = new List<string> { Type, _server.Os, _server.HostingType, _server.Environment };
				tags.AddRange(_server.Capabilities);
				tags.AddRange(_server.UsedPorts.Select(port => $"port:{port}"));
				return string.Join(" ", tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));
			}

			if (_dataService is not null)
			{
				return string.Join(" ", new[]
				{
					Type,
					_dataService.Kind,
					_dataService.Environment,
					_dataService.Version,
					_dataService.Size,
					_dataService.StorageGb is { } storage ? $"{storage}GB" : null,
				}.Where(tag => !string.IsNullOrWhiteSpace(tag)));
			}

			return string.Empty;
		}
	}

	public bool CanAddCredential => _server is not null;

	public bool HasCredentials => _server?.HasCredentials == true;

	public IEnumerable<CredentialRowViewModel> Credentials => _server?.Credentials ?? [];

	public IRelayCommand EditCommand => _server?.OpenEditCommand ?? _dataService!.EditCommand;

	public IAsyncRelayCommand DiscoverCommand => _server?.DiscoverInventoryCommand ?? _dataService!.DiscoverCommand;

	public IRelayCommand? AddCredentialCommand => _server?.OpenAddCredentialCommand;
}

public sealed partial class DataServiceRowViewModel : ObservableObject
{
	private readonly Guid _dataServiceId;
	private readonly IIrisApiClient _api;
	private readonly ServersViewModel _parent;

	public DataServiceRowViewModel(DataServiceResponse dataService, IIrisApiClient api, ServersViewModel parent)
	{
		_dataServiceId = dataService.Id;
		_api = api;
		_parent = parent;
		ApplyFrom(dataService);
	}

	public IReadOnlyList<string> KindOptions => _parent.DataServiceKindOptions;

	public IReadOnlyList<string> EnvironmentOptions => _parent.EnvironmentOptions;

	[ObservableProperty] private string _name = string.Empty;
	[ObservableProperty] private string _kind = "Mssql";
	[ObservableProperty] private string _endpoint = string.Empty;
	[ObservableProperty] private int? _port;
	[ObservableProperty] private string? _username;
	[ObservableProperty] private string? _version;
	[ObservableProperty] private string? _size;
	[ObservableProperty] private int? _storageGb;
	[ObservableProperty] private string _environment = "Test";
	[ObservableProperty] private bool _isActive;
	[ObservableProperty] private string _editName = string.Empty;
	[ObservableProperty] private string _editKind = "Mssql";
	[ObservableProperty] private string _editEndpoint = string.Empty;
	[ObservableProperty] private string _editPort = string.Empty;
	[ObservableProperty] private string _editUsername = string.Empty;
	[ObservableProperty] private string _editPassword = string.Empty;
	[ObservableProperty] private string _editVersion = string.Empty;
	[ObservableProperty] private string _editSize = string.Empty;
	[ObservableProperty] private string _editStorageGb = string.Empty;
	[ObservableProperty] private string _editEnvironment = "Test";
	[ObservableProperty] private bool _editActive;
	[ObservableProperty] private bool _isEditMode;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _error;

	public bool HasError => !string.IsNullOrEmpty(Error);

	public string EndpointSummary => Port is { } port ? $"{Endpoint}:{port}" : Endpoint;

	public string CredentialSummary => string.IsNullOrWhiteSpace(Username)
		? "No credential"
		: $"credential: {Username}";

	public string DetailsSummary
	{
		get
		{
			var parts = new List<string>();
			if (!string.IsNullOrWhiteSpace(Version))
			{
				parts.Add(Version);
			}

			if (!string.IsNullOrWhiteSpace(Size))
			{
				parts.Add(Size);
			}

			if (StorageGb is { } storage)
			{
				parts.Add($"{storage} GB");
			}

			return parts.Count == 0 ? "No sizing details yet" : string.Join(" | ", parts);
		}
	}

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnEditKindChanged(string value)
	{
		if (string.IsNullOrWhiteSpace(EditPort) || EditPort is "1433" or "5432" or "6379")
		{
			EditPort = ServersViewModel.DefaultDataServicePort(value);
		}
	}

	private void ApplyFrom(DataServiceResponse dataService)
	{
		Name = dataService.Name;
		Kind = dataService.Kind;
		Endpoint = dataService.Endpoint;
		Port = dataService.Port;
		Username = dataService.Username;
		Version = dataService.Version;
		Size = dataService.Size;
		StorageGb = dataService.StorageGb;
		Environment = dataService.Environment;
		IsActive = dataService.IsActive;
		OnPropertyChanged(nameof(EndpointSummary));
		OnPropertyChanged(nameof(CredentialSummary));
		OnPropertyChanged(nameof(DetailsSummary));
	}

	[RelayCommand]
	private void Edit()
	{
		EditName = Name;
		EditKind = Kind;
		EditEndpoint = Endpoint;
		EditPort = Port?.ToString() ?? string.Empty;
		EditUsername = Username ?? string.Empty;
		EditPassword = string.Empty;
		EditVersion = Version ?? string.Empty;
		EditSize = Size ?? string.Empty;
		EditStorageGb = StorageGb?.ToString() ?? string.Empty;
		EditEnvironment = Environment;
		EditActive = IsActive;
		Error = null;
		IsEditMode = true;
	}

	[RelayCommand]
	private void CancelEdit()
	{
		Error = null;
		IsEditMode = false;
	}

	[RelayCommand]
	private async Task SaveAsync()
	{
		var name = EditName.Trim();
		var endpoint = EditEndpoint.Trim();
		var username = EditUsername.Trim();
		if (name.Length == 0 || endpoint.Length == 0 || username.Length == 0)
		{
			Error = "Name, endpoint and username are required.";
			return;
		}

		if (!ServersViewModel.TryParseOptionalInt(EditPort, "Port", allowZero: false, max: 65535, out var port, out var parseError) ||
			!ServersViewModel.TryParseOptionalInt(EditStorageGb, "Storage GB", allowZero: true, max: null, out var storageGb, out parseError))
		{
			Error = parseError;
			return;
		}

		IsBusy = true;
		Error = null;

		try
		{
			var updated = await _api.UpdateDataServiceAsync(_dataServiceId, new UpsertDataServiceRequest(
				name,
				EditKind,
				endpoint,
				port,
				string.IsNullOrWhiteSpace(EditVersion) ? null : EditVersion.Trim(),
				string.IsNullOrWhiteSpace(EditSize) ? null : EditSize.Trim(),
				storageGb,
				EditEnvironment,
				EditActive,
				username,
				string.IsNullOrEmpty(EditPassword) ? null : EditPassword));

			ApplyFrom(updated);
			IsEditMode = false;
			_parent.RebuildResources();
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

	[RelayCommand]
	private async Task DiscoverAsync()
	{
		IsBusy = true;
		Error = null;

		try
		{
			var updated = await _api.DiscoverDataServiceInventoryAsync(_dataServiceId);
			ApplyFrom(updated);
			_parent.RebuildResources();
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
}

/// <summary>A pickable Iris user (or the "— not linked —" sentinel with an empty id).</summary>
public sealed record UserOption(Guid Id, string Display)
{
	public static readonly UserOption None = new(Guid.Empty, "— not linked —");

	public override string ToString() => Display;
}

/// <summary>
/// The credential fields shared by the "new server" panel and the per-row "add credential" panel:
/// username + Password/SshKey secret, plus the SystemUser/ServiceAccount classification.
/// </summary>
public sealed partial class CredentialFormViewModel : ObservableObject
{
	private readonly bool _isEditMode;
	private readonly bool _canManageSecrets;

	public CredentialFormViewModel(
		IReadOnlyList<UserOption> ownerOptions,
		bool isEditMode = false,
		bool canManageSecrets = false)
	{
		OwnerOptions = ownerOptions;
		_isEditMode = isEditMode;
		_canManageSecrets = canManageSecrets;
		_selectedOwner = ownerOptions.Count > 0 ? ownerOptions[0] : UserOption.None;
	}

	/// <summary>
	/// The secret input is always available when creating a credential. When editing an existing
	/// one it stays hidden unless the caller holds <c>infrastructure.secrets.manage</c> (lead role) —
	/// only they may rotate/replace the stored password or key.
	/// </summary>
	public bool ShowSecretField => !_isEditMode || _canManageSecrets;

	public IReadOnlyList<UserOption> OwnerOptions { get; }

	public IReadOnlyList<string> AuthMethods { get; } = ["Password", "SshKey"];

	public IReadOnlyList<string> KindOptions { get; } = ["SystemUser", "ServiceAccount"];

	[ObservableProperty] private string _username = string.Empty;
	[ObservableProperty] private string _authMethod = "Password";
	[ObservableProperty] private string _secretValue = string.Empty;
	[ObservableProperty] private string _kind = "SystemUser";
	[ObservableProperty] private UserOption _selectedOwner;
	[ObservableProperty] private string _serviceName = string.Empty;
	[ObservableProperty] private string _label = string.Empty;

	public bool IsSystemUser => Kind == "SystemUser";

	public bool IsServiceAccount => Kind == "ServiceAccount";

	/// <summary>Password auth → a masked single-line secret field.</summary>
	public bool IsPasswordAuth => AuthMethod == "Password";

	/// <summary>SSH-key auth → a multi-line (text-area) secret field, not masked.</summary>
	public bool IsSshKeyAuth => AuthMethod == "SshKey";

	partial void OnAuthMethodChanged(string value)
	{
		OnPropertyChanged(nameof(IsPasswordAuth));
		OnPropertyChanged(nameof(IsSshKeyAuth));
		// A password and an SSH key aren't interchangeable text — start the field fresh.
		SecretValue = string.Empty;
	}

	partial void OnKindChanged(string value)
	{
		OnPropertyChanged(nameof(IsSystemUser));
		OnPropertyChanged(nameof(IsServiceAccount));
		if (IsServiceAccount)
		{
			SelectedOwner = OwnerOptions.Count > 0 ? OwnerOptions[0] : UserOption.None;
		}
		else
		{
			ServiceName = string.Empty;
		}
	}

	public void Reset()
	{
		Username = string.Empty;
		AuthMethod = "Password";
		SecretValue = string.Empty;
		Kind = "SystemUser";
		SelectedOwner = OwnerOptions.Count > 0 ? OwnerOptions[0] : UserOption.None;
		ServiceName = string.Empty;
		Label = string.Empty;
	}

	public bool TryBuild(out ServerCredentialInputRequest? request, out string? error)
	{
		request = null;
		error = null;

		var username = Username.Trim();
		if (username.Length == 0)
		{
			error = "Credential username is required.";
			return false;
		}

		if (ShowSecretField && string.IsNullOrEmpty(SecretValue))
		{
			error = "Enter a password or SSH private key for the credential.";
			return false;
		}

		Guid? ownerUserId = null;
		string? serviceName = null;

		if (IsServiceAccount)
		{
			var svc = ServiceName.Trim();
			if (svc.Length == 0)
			{
				error = "A service account needs a service name (e.g. 'ansible').";
				return false;
			}

			serviceName = svc;
		}
		else if (SelectedOwner is { Id: var id } && id != Guid.Empty)
		{
			ownerUserId = id;
		}

		request = new ServerCredentialInputRequest(
			username,
			AuthMethod,
			SecretValue,
			Kind,
			ownerUserId,
			serviceName,
			string.IsNullOrWhiteSpace(Label) ? null : Label.Trim());
		return true;
	}
}

/// <summary>One server row: its identity/network details, plus the inline "add credential" form.</summary>
public sealed partial class ServerRowViewModel : ObservableObject, IConfirmDeletable
{
	private readonly Guid _serverId;
	private readonly IIrisApiClient _api;
	private readonly ServersViewModel _parent;

	public ServerRowViewModel(
		ServerResponse server,
		IIrisApiClient api,
		IReadOnlyList<UserOption> ownerOptions,
		ServersViewModel parent)
	{
		_serverId = server.Id;
		_api = api;
		_parent = parent;
		AddCredentialForm = parent.NewCredentialForm(isEdit: false);
		Credentials = new ObservableCollection<CredentialRowViewModel>(
			server.Credentials.Select(c => new CredentialRowViewModel(c, this)));
		ApplyFrom(server);
	}

	[ObservableProperty] private string _name = string.Empty;
	[ObservableProperty] private string? _hostname;
	[ObservableProperty] private string _os = "Linux";
	[ObservableProperty] private string? _osVersion;
	[ObservableProperty] private string? _machineSize;
	[ObservableProperty] private string _hostingType = "SelfHosted";
	[ObservableProperty] private string? _publicIpAddress;
	[ObservableProperty] private string? _privateIpAddress;
	[ObservableProperty] private string _environment = "Test";
	[ObservableProperty] private bool _isActive = true;
	[ObservableProperty] private IReadOnlyList<string> _capabilities = [];
	[ObservableProperty] private IReadOnlyList<int> _usedPorts = [];
	[ObservableProperty] private int? _cpuCores;
	[ObservableProperty] private int? _memoryMb;
	[ObservableProperty] private int? _diskGb;
	[ObservableProperty] private int? _applicationDiskGb;
	[ObservableProperty] private int? _backupDiskGb;

	public ObservableCollection<CredentialRowViewModel> Credentials { get; }

	public CredentialFormViewModel AddCredentialForm { get; }

	public IReadOnlyList<string> OsOptions => _parent.OsOptions;

	public IReadOnlyList<string> HostingTypeOptions => _parent.HostingTypeOptions;

	public IReadOnlyList<string> EnvironmentOptions => _parent.EnvironmentOptions;

	public bool HasResourceSummary =>
		CpuCores.HasValue ||
		MemoryMb.HasValue ||
		DiskGb.HasValue ||
		ApplicationDiskGb.HasValue ||
		BackupDiskGb.HasValue;

	public string ResourceSummary
	{
		get
		{
			var parts = new List<string>();
			if (CpuCores is { } cpu)
			{
				parts.Add($"{cpu} CPU");
			}

			if (MemoryMb is { } memory)
			{
				parts.Add($"{memory} MB RAM");
			}

			if (DiskGb is { } disk)
			{
				parts.Add($"{disk} GB disk");
			}

			if (ApplicationDiskGb is { } appDisk)
			{
				parts.Add($"{appDisk} GB apps");
			}

			if (BackupDiskGb is { } backupDisk)
			{
				parts.Add($"{backupDisk} GB backup");
			}

			return string.Join(" | ", parts);
		}
	}

	public string UsedPortsText => UsedPorts.Count == 0
		? "No known used ports"
		: $"Used ports: {string.Join(", ", UsedPorts)}";

	public bool HasDiscoveryDetails =>
		!string.IsNullOrWhiteSpace(OsVersion) ||
		!string.IsNullOrWhiteSpace(MachineSize);

	public string DiscoverySummary => string.Join(" | ", new[] { OsVersion, MachineSize }
		.Where(value => !string.IsNullOrWhiteSpace(value)));

	public bool HasCredentials => Credentials.Count > 0;

	private void ApplyFrom(ServerResponse server)
	{
		Name = server.Name;
		Hostname = server.Hostname;
		Os = server.Os;
		OsVersion = server.OsVersion;
		MachineSize = server.MachineSize;
		HostingType = server.HostingType;
		PublicIpAddress = server.PublicIpAddress;
		PrivateIpAddress = server.PrivateIpAddress;
		Environment = server.Environment;
		IsActive = server.IsActive;
		Capabilities = server.Capabilities.ToArray();
		UsedPorts = server.UsedPorts.ToArray();
		CpuCores = server.Resources?.CpuCores;
		MemoryMb = server.Resources?.MemoryMb;
		DiskGb = server.Resources?.DiskGb;
		ApplicationDiskGb = server.Resources?.ApplicationDiskGb;
		BackupDiskGb = server.Resources?.BackupDiskGb;
		OnPropertyChanged(nameof(HasResourceSummary));
		OnPropertyChanged(nameof(ResourceSummary));
		OnPropertyChanged(nameof(UsedPortsText));
		OnPropertyChanged(nameof(HasDiscoveryDetails));
		OnPropertyChanged(nameof(DiscoverySummary));
	}

	// ----- Add credential -----

	/// <summary>Raised after a credential is added so its dialog window can close.</summary>
	public event EventHandler? AddCredentialCompleted;

	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _credentialError;

	public bool HasCredentialError => !string.IsNullOrEmpty(CredentialError);

	partial void OnCredentialErrorChanged(string? value) => OnPropertyChanged(nameof(HasCredentialError));

	[RelayCommand]
	private void OpenAddCredential() => _parent.OpenCredentialPanel(this);

	// ----- Edit / delete -----

	/// <summary>Raised after the server is saved so its edit dialog window can close.</summary>
	public event EventHandler? EditCompleted;

	/// <summary>Raised after the server is deleted so the confirm-delete window can close.</summary>
	public event EventHandler? DeleteCompleted;

	/// <summary>Raised from the edit dialog to close it before the confirm-delete window opens.</summary>
	public event EventHandler? DeleteRequested;

	string IConfirmDeletable.DeleteDialogTitle => "Delete server";

	string IConfirmDeletable.DeleteDialogPrompt =>
		"This permanently removes the server and purges every stored credential secret. It cannot be undone.";

	string IConfirmDeletable.DeleteTargetName => Name;

	[ObservableProperty] private string _editName = string.Empty;
	[ObservableProperty] private string _editHostname = string.Empty;
	[ObservableProperty] private string _editOs = "Linux";
	[ObservableProperty] private string _editHostingType = "SelfHosted";
	[ObservableProperty] private string _editEnvironment = "Test";
	[ObservableProperty] private string _editPublicIp = string.Empty;
	[ObservableProperty] private string _editPrivateIp = string.Empty;
	[ObservableProperty] private string _editCpuCores = string.Empty;
	[ObservableProperty] private string _editMemoryMb = string.Empty;
	[ObservableProperty] private string _editDiskGb = string.Empty;
	[ObservableProperty] private string _editApplicationDiskGb = string.Empty;
	[ObservableProperty] private string _editBackupDiskGb = string.Empty;
	[ObservableProperty] private string _editUsedPorts = string.Empty;
	[ObservableProperty] private string _deleteConfirmName = string.Empty;
	[ObservableProperty] private bool _isEditBusy;
	[ObservableProperty] private string? _editError;

	public bool HasEditError => !string.IsNullOrEmpty(EditError);

	/// <summary>Delete is armed only once the operator has typed the server name exactly.</summary>
	public bool CanDelete => string.Equals(DeleteConfirmName.Trim(), Name, StringComparison.Ordinal);

	partial void OnEditErrorChanged(string? value) => OnPropertyChanged(nameof(HasEditError));

	partial void OnNameChanged(string value) => OnPropertyChanged(nameof(CanDelete));

	partial void OnDeleteConfirmNameChanged(string value)
	{
		OnPropertyChanged(nameof(CanDelete));
		DeleteCommand.NotifyCanExecuteChanged();
	}

	// ----- Concurrent-edit lock -----

	private const string LockResource = "server";
	private const int HeartbeatSeconds = 45;
	private CancellationTokenSource? _heartbeatCts;

	[ObservableProperty] private string? _editLockNotice;

	/// <summary>Set when someone else holds the edit lock — the row shows it and the editor stays shut.</summary>
	public bool HasEditLockNotice => !string.IsNullOrEmpty(EditLockNotice);

	partial void OnEditLockNoticeChanged(string? value) => OnPropertyChanged(nameof(HasEditLockNotice));

	[RelayCommand]
	private async Task OpenEditAsync()
	{
		EditLockNotice = null;
		EditError = null;

		try
		{
			var slot = await _api.AcquireEditLockAsync(LockResource, _serverId);
			if (!slot.Mine)
			{
				EditLockNotice = $"{slot.HolderDisplayName} is editing this server right now — try again in a moment.";
				return;
			}
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			EditLockNotice = ex.Message;
			return;
		}

		StartHeartbeat();

		EditName = Name;
		EditHostname = Hostname ?? string.Empty;
		EditOs = Os;
		EditHostingType = HostingType;
		EditEnvironment = Environment;
		EditPublicIp = PublicIpAddress ?? string.Empty;
		EditPrivateIp = PrivateIpAddress ?? string.Empty;
		EditCpuCores = CpuCores?.ToString() ?? string.Empty;
		EditMemoryMb = MemoryMb?.ToString() ?? string.Empty;
		EditDiskGb = DiskGb?.ToString() ?? string.Empty;
		EditApplicationDiskGb = ApplicationDiskGb?.ToString() ?? string.Empty;
		EditBackupDiskGb = BackupDiskGb?.ToString() ?? string.Empty;
		EditUsedPorts = string.Join(", ", UsedPorts);
		DeleteConfirmName = string.Empty;
		_parent.RaiseEditRequested(this);
	}

	private void StartHeartbeat()
	{
		_heartbeatCts?.Cancel();
		var cts = new CancellationTokenSource();
		_heartbeatCts = cts;
		_ = HeartbeatAsync(cts.Token);
	}

	private async Task HeartbeatAsync(CancellationToken token)
	{
		try
		{
			using var timer = new PeriodicTimer(TimeSpan.FromSeconds(HeartbeatSeconds));
			while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
			{
				try
				{
					await _api.AcquireEditLockAsync(LockResource, _serverId, token).ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
				{
					// A dropped heartbeat just lets the lock lapse sooner; nothing to surface.
				}
			}
		}
		catch (OperationCanceledException)
		{
			// editor closed
		}
	}

	/// <summary>Called by the edit dialog as it closes: stop the heartbeat and release the lock.</summary>
	public void NotifyEditorClosed()
	{
		if (_heartbeatCts is null)
		{
			return;
		}

		_heartbeatCts.Cancel();
		_heartbeatCts.Dispose();
		_heartbeatCts = null;
		_ = SafeReleaseLockAsync();
	}

	private async Task SafeReleaseLockAsync()
	{
		try
		{
			await _api.ReleaseEditLockAsync(LockResource, _serverId).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			// The lock will expire on its own if the release didn't land.
		}
	}

	/// <summary>From the edit dialog: close it and hand off to the dedicated confirm-delete window.</summary>
	[RelayCommand]
	private void RequestDelete()
	{
		DeleteConfirmName = string.Empty;
		EditError = null;
		DeleteRequested?.Invoke(this, EventArgs.Empty);
		_parent.RaiseDeleteRequested(this);
	}

	[RelayCommand]
	private async Task SaveEditAsync()
	{
		var name = EditName.Trim();
		if (name.Length == 0)
		{
			EditError = "Server name is required.";
			return;
		}

		var publicIp = EditPublicIp.Trim();
		var privateIp = EditPrivateIp.Trim();
		if (publicIp.Length == 0 && privateIp.Length == 0)
		{
			EditError = "Enter at least a public or a private IP address.";
			return;
		}

		if (!TryBuildResourceProfile(out var resources, out var resourceError))
		{
			EditError = resourceError;
			return;
		}

		if (!TryParsePorts(out var ports, out var portsError))
		{
			EditError = portsError;
			return;
		}

		IsEditBusy = true;
		EditError = null;

		try
		{
			await _api.UpdateServerAsync(_serverId, new UpdateServerRequest(
				name,
				string.IsNullOrWhiteSpace(EditHostname) ? null : EditHostname.Trim(),
				EditOs,
				EditHostingType,
				publicIp.Length == 0 ? null : publicIp,
				privateIp.Length == 0 ? null : privateIp,
				EditEnvironment));

			var withCapacity = await _api.UpdateServerCapacityAsync(_serverId, new UpdateServerCapacityRequest(
				Capabilities,
				resources,
				ports));

			ApplyFrom(withCapacity);
			_parent.RebuildResources();
			EditCompleted?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			EditError = ex.Message;
		}
		finally
		{
			IsEditBusy = false;
		}
	}

	private bool TryBuildResourceProfile(out ResourceProfileRequest? resources, out string? error)
	{
		resources = null;
		error = null;

		if (!TryParseOptionalInt(EditCpuCores, "CPU cores", out var cpuCores, out error) ||
			!TryParseOptionalInt(EditMemoryMb, "Memory MB", out var memoryMb, out error) ||
			!TryParseOptionalInt(EditDiskGb, "Disk GB", out var diskGb, out error) ||
			!TryParseOptionalInt(EditApplicationDiskGb, "Application disk GB", out var applicationDiskGb, out error) ||
			!TryParseOptionalInt(EditBackupDiskGb, "Backup disk GB", out var backupDiskGb, out error))
		{
			return false;
		}

		if (diskGb is { } total &&
			applicationDiskGb is { } appDisk &&
			backupDiskGb is { } backupDisk &&
			appDisk + backupDisk > total)
		{
			error = "Application disk and backup disk cannot exceed total disk.";
			return false;
		}

		if (cpuCores.HasValue ||
			memoryMb.HasValue ||
			diskGb.HasValue ||
			applicationDiskGb.HasValue ||
			backupDiskGb.HasValue)
		{
			resources = new ResourceProfileRequest(cpuCores, memoryMb, diskGb, applicationDiskGb, backupDiskGb);
		}

		return true;
	}

	private static bool TryParseOptionalInt(string value, string label, out int? result, out string? error)
	{
		result = null;
		error = null;

		var trimmed = value.Trim();
		if (trimmed.Length == 0)
		{
			return true;
		}

		if (!int.TryParse(trimmed, out var parsed))
		{
			error = $"{label} must be a whole number.";
			return false;
		}

		if (parsed < 0)
		{
			error = $"{label} cannot be negative.";
			return false;
		}

		result = parsed;
		return true;
	}

	private bool TryParsePorts(out IReadOnlyList<int> ports, out string? error)
	{
		ports = [];
		error = null;

		var text = EditUsedPorts.Trim();
		if (text.Length == 0)
		{
			return true;
		}

		var parsed = new List<int>();
		foreach (var item in text.Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
		{
			if (!int.TryParse(item, out var port) || port is < 1 or > 65535)
			{
				error = "Used ports must be numbers between 1 and 65535.";
				return false;
			}

			parsed.Add(port);
		}

		ports = parsed.Distinct().Order().ToArray();
		return true;
	}

	[RelayCommand(CanExecute = nameof(CanDelete))]
	private async Task DeleteAsync()
	{
		IsEditBusy = true;
		EditError = null;

		try
		{
			await _api.DeleteServerAsync(_serverId);
			_parent.RemoveRow(this);
			DeleteCompleted?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			EditError = ex.Message;
		}
		finally
		{
			IsEditBusy = false;
		}
	}

	[RelayCommand]
	private async Task AddCredentialAsync()
	{
		if (!AddCredentialForm.TryBuild(out var input, out var error))
		{
			CredentialError = error;
			return;
		}

		IsBusy = true;
		CredentialError = null;

		try
		{
			var request = new AddServerCredentialRequest(
				input!.Username, input.AuthMethod, input.SecretValue,
				input.Kind, input.OwnerUserId, input.ServiceName, input.Label);
			var result = await _api.AddServerCredentialAsync(_serverId, request);
			Credentials.Add(new CredentialRowViewModel(result, this));
			OnPropertyChanged(nameof(HasCredentials));
			_parent.RebuildResources();
			await DiscoverInventoryAsync();

			AddCredentialCompleted?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			CredentialError = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private async Task RemoveCredentialAsync(CredentialRowViewModel? credential)
	{
		if (credential is null)
		{
			return;
		}

		IsBusy = true;
		CredentialError = null;

		try
		{
			await _api.RemoveServerCredentialAsync(_serverId, credential.Id);
			Credentials.Remove(credential);
			OnPropertyChanged(nameof(HasCredentials));
			_parent.RebuildResources();
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			CredentialError = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	public async Task DiscoverInventoryAsync()
	{
		if (!HasCredentials)
		{
			CredentialError = "Add at least one credential before discovering inventory.";
			return;
		}

		IsBusy = true;
		CredentialError = null;

		try
		{
			var updated = await _api.DiscoverServerInventoryAsync(_serverId);
			ApplyFrom(updated);
			_parent.RebuildResources();
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			CredentialError = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}
}

/// <summary>
/// One OS-login credential. A thin UI wrapper around <see cref="ServerCredentialResponse"/> carrying a
/// back-reference to its owning <see cref="ServerRowViewModel"/> for the nested "Revoke" button.
/// Never carries a secret value — the API never returns one.
/// </summary>
public sealed class CredentialRowViewModel(ServerCredentialResponse credential, ServerRowViewModel owner)
{
	public Guid Id => credential.Id;

	public string Username => credential.Username;

	public string AuthMethod => credential.AuthMethod;

	public string Kind => credential.Kind;

	/// <summary>Human-readable subtitle: the linked Iris user, the service name, or the free-text label.</summary>
	public string Detail => credential.Kind == "ServiceAccount"
		? $"service · {credential.ServiceName}"
		: credential.OwnerDisplayName is { Length: > 0 } ownerName
			? $"system user · {ownerName}"
			: credential.Label ?? "system user";

	public ServerRowViewModel Owner => owner;
}
