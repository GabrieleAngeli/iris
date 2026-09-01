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

	[ObservableProperty] private bool _isNewServerPanelOpen;
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
	private void ToggleNewServerPanel() => IsNewServerPanelOpen = !IsNewServerPanelOpen;

	// ----- Add-credential modal (shared across server rows) -----

	[ObservableProperty] private ServerRowViewModel? _activeCredentialRow;

	public bool IsCredentialModalOpen => ActiveCredentialRow is not null;

	partial void OnActiveCredentialRowChanged(ServerRowViewModel? value) =>
		OnPropertyChanged(nameof(IsCredentialModalOpen));

	public void OpenCredentialPanel(ServerRowViewModel row)
	{
		row.AddCredentialForm.Reset();
		row.CredentialError = null;
		ActiveCredentialRow = row;
	}

	[RelayCommand]
	private void CloseCredentialPanel() => ActiveCredentialRow = null;

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

			IsNewServerPanelOpen = false;
			if (created.Credentials.Count == 0)
			{
				OpenCredentialPanel(row);
			}

			NewServerName = string.Empty;
			NewServerHostname = string.Empty;
			NewServerPublicIp = string.Empty;
			NewServerPrivateIp = string.Empty;
			NewServerOs = "Linux";
			NewServerHostingType = "SelfHosted";
			NewServerEnvironment = "Test";
			NewServerCredential.Reset();
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
		Name = server.Name;
		Hostname = server.Hostname;
		Os = server.Os;
		HostingType = server.HostingType;
		PublicIpAddress = server.PublicIpAddress;
		PrivateIpAddress = server.PrivateIpAddress;
		Environment = server.Environment;
		AddCredentialForm = new CredentialFormViewModel(ownerOptions);
		Credentials = new ObservableCollection<CredentialRowViewModel>(
			server.Credentials.Select(c => new CredentialRowViewModel(c, this)));
	}

	public string Name { get; }

	public string? Hostname { get; }

	public string Os { get; }

	public string HostingType { get; }

	public string? PublicIpAddress { get; }

	public string? PrivateIpAddress { get; }

	public string Environment { get; }

	public ObservableCollection<CredentialRowViewModel> Credentials { get; }

	public CredentialFormViewModel AddCredentialForm { get; }

	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _credentialError;

	public bool HasCredentialError => !string.IsNullOrEmpty(CredentialError);

	partial void OnCredentialErrorChanged(string? value) => OnPropertyChanged(nameof(HasCredentialError));

	[RelayCommand]
	private void OpenAddCredential() => _parent.OpenCredentialPanel(this);

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

			_parent.ActiveCredentialRow = null;
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
