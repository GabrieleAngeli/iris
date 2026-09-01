using System.Collections.ObjectModel;
using Iris.Contracts.Infrastructure;

namespace Iris.App.ViewModels;

/// <summary>Infrastructure › Servers: registered servers and the OS-login credentials each holds.</summary>
public partial class ServersViewModel : ObservableObject
{
	private readonly IIrisApiClient _api;

	public ServersViewModel(IIrisApiClient api)
	{
		_api = api;
		NewServerCredential = new CredentialFormViewModel(OwnerOptions);
	}

	public ObservableCollection<ServerRowViewModel> Servers { get; } = [];

	/// <summary>Iris users offered as the "owner" of a system-user credential. Shared with every credential form.</summary>
	public ObservableCollection<UserOption> OwnerOptions { get; } = [UserOption.None];

	[ObservableProperty] private bool _isLoading;
	[ObservableProperty] private string? _error;

	public bool HasError => !string.IsNullOrEmpty(Error);

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
			await LoadOwnerOptionsAsync();

			var servers = await _api.GetServersAsync();
			Servers.Clear();
			foreach (var server in servers)
			{
				Servers.Add(new ServerRowViewModel(server, _api, OwnerOptions, this));
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

	/// <summary>Raised when the operator asks to register a server — the page opens the dialog window.</summary>
	public event EventHandler? NewServerRequested;

	/// <summary>Raised after a server is registered so its dialog window can close.</summary>
	public event EventHandler? NewServerCompleted;

	/// <summary>Raised (post-create, or from a row's button) to open the add-credential dialog for a server.</summary>
	public event EventHandler<ServerRowViewModel>? AddCredentialRequested;

	/// <summary>Raised from a row's edit button to open the edit/delete dialog for a server.</summary>
	public event EventHandler<ServerRowViewModel>? EditServerRequested;

	public void RaiseEditRequested(ServerRowViewModel row) => EditServerRequested?.Invoke(this, row);

	public void RemoveRow(ServerRowViewModel row) => Servers.Remove(row);

	[ObservableProperty] private string _newServerName = string.Empty;
	[ObservableProperty] private string _newServerHostname = string.Empty;
	[ObservableProperty] private string _newServerOs = "Linux";
	[ObservableProperty] private string _newServerHostingType = "SelfHosted";
	[ObservableProperty] private string _newServerEnvironment = "Test";
	[ObservableProperty] private string _newServerPublicIp = string.Empty;
	[ObservableProperty] private string _newServerPrivateIp = string.Empty;
	[ObservableProperty] private bool _includeCredential = true;
	[ObservableProperty] private bool _isCreatingServer;
	[ObservableProperty] private string? _createServerError;

	public CredentialFormViewModel NewServerCredential { get; }

	public bool HasCreateServerError => !string.IsNullOrEmpty(CreateServerError);

	partial void OnCreateServerErrorChanged(string? value) => OnPropertyChanged(nameof(HasCreateServerError));

	[RelayCommand]
	private void RequestNewServer()
	{
		NewServerName = string.Empty;
		NewServerHostname = string.Empty;
		NewServerPublicIp = string.Empty;
		NewServerPrivateIp = string.Empty;
		NewServerOs = "Linux";
		NewServerHostingType = "SelfHosted";
		NewServerEnvironment = "Test";
		IncludeCredential = true;
		CreateServerError = null;
		NewServerCredential.Reset();
		NewServerRequested?.Invoke(this, EventArgs.Empty);
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

			NewServerCompleted?.Invoke(this, EventArgs.Empty);

			// A server without a way in isn't useful — chain into the add-credential dialog, but on
			// the next UI tick so the new-server window finishes closing before the next one opens.
			if (created.Credentials.Count == 0)
			{
				var created_row = row;
				MainThread.BeginInvokeOnMainThread(() => OpenCredentialPanel(created_row));
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
	public CredentialFormViewModel(IReadOnlyList<UserOption> ownerOptions)
	{
		OwnerOptions = ownerOptions;
		_selectedOwner = ownerOptions.Count > 0 ? ownerOptions[0] : UserOption.None;
	}

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

		if (string.IsNullOrEmpty(SecretValue))
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
public sealed partial class ServerRowViewModel : ObservableObject
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
		AddCredentialForm = new CredentialFormViewModel(ownerOptions);
		Credentials = new ObservableCollection<CredentialRowViewModel>(
			server.Credentials.Select(c => new CredentialRowViewModel(c, this)));
		ApplyFrom(server);
	}

	[ObservableProperty] private string _name = string.Empty;
	[ObservableProperty] private string? _hostname;
	[ObservableProperty] private string _os = "Linux";
	[ObservableProperty] private string _hostingType = "SelfHosted";
	[ObservableProperty] private string? _publicIpAddress;
	[ObservableProperty] private string? _privateIpAddress;
	[ObservableProperty] private string _environment = "Test";

	public ObservableCollection<CredentialRowViewModel> Credentials { get; }

	public CredentialFormViewModel AddCredentialForm { get; }

	public IReadOnlyList<string> OsOptions => _parent.OsOptions;

	public IReadOnlyList<string> HostingTypeOptions => _parent.HostingTypeOptions;

	public IReadOnlyList<string> EnvironmentOptions => _parent.EnvironmentOptions;

	private void ApplyFrom(ServerResponse server)
	{
		Name = server.Name;
		Hostname = server.Hostname;
		Os = server.Os;
		HostingType = server.HostingType;
		PublicIpAddress = server.PublicIpAddress;
		PrivateIpAddress = server.PrivateIpAddress;
		Environment = server.Environment;
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

	/// <summary>Raised after the server is saved or deleted so its edit dialog window can close.</summary>
	public event EventHandler? EditCompleted;

	[ObservableProperty] private string _editName = string.Empty;
	[ObservableProperty] private string _editHostname = string.Empty;
	[ObservableProperty] private string _editOs = "Linux";
	[ObservableProperty] private string _editHostingType = "SelfHosted";
	[ObservableProperty] private string _editEnvironment = "Test";
	[ObservableProperty] private string _editPublicIp = string.Empty;
	[ObservableProperty] private string _editPrivateIp = string.Empty;
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

	[RelayCommand]
	private void OpenEdit()
	{
		EditName = Name;
		EditHostname = Hostname ?? string.Empty;
		EditOs = Os;
		EditHostingType = HostingType;
		EditEnvironment = Environment;
		EditPublicIp = PublicIpAddress ?? string.Empty;
		EditPrivateIp = PrivateIpAddress ?? string.Empty;
		DeleteConfirmName = string.Empty;
		EditError = null;
		_parent.RaiseEditRequested(this);
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

		IsEditBusy = true;
		EditError = null;

		try
		{
			var updated = await _api.UpdateServerAsync(_serverId, new UpdateServerRequest(
				name,
				string.IsNullOrWhiteSpace(EditHostname) ? null : EditHostname.Trim(),
				EditOs,
				EditHostingType,
				publicIp.Length == 0 ? null : publicIp,
				privateIp.Length == 0 ? null : privateIp,
				EditEnvironment));

			ApplyFrom(updated);
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

	[RelayCommand(CanExecute = nameof(CanDelete))]
	private async Task DeleteAsync()
	{
		IsEditBusy = true;
		EditError = null;

		try
		{
			await _api.DeleteServerAsync(_serverId);
			_parent.RemoveRow(this);
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
