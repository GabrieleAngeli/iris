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
	}

	public ObservableCollection<ServerRowViewModel> Servers { get; } = [];

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
			var servers = await _api.GetServersAsync();

			Servers.Clear();
			foreach (var server in servers)
			{
				Servers.Add(new ServerRowViewModel(server, _api));
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
	[ObservableProperty] private bool _isCreatingServer;
	[ObservableProperty] private string? _createServerError;

	public bool HasCreateServerError => !string.IsNullOrEmpty(CreateServerError);

	partial void OnCreateServerErrorChanged(string? value) => OnPropertyChanged(nameof(HasCreateServerError));

	[RelayCommand]
	private void ToggleNewServerPanel() => IsNewServerPanelOpen = !IsNewServerPanelOpen;

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
				NewServerEnvironment);

			var created = await _api.CreateServerAsync(request);

			var row = new ServerRowViewModel(created, _api)
			{
				// Registering a server is only useful once it has a way in — jump
				// straight into the "add credential" panel, mirroring how creating a
				// user jumps straight into assigning its first role.
				IsAddCredentialPanelOpen = true,
			};
			Servers.Insert(0, row);

			IsNewServerPanelOpen = false;
			NewServerName = string.Empty;
			NewServerHostname = string.Empty;
			NewServerPublicIp = string.Empty;
			NewServerPrivateIp = string.Empty;
			NewServerOs = "Linux";
			NewServerHostingType = "SelfHosted";
			NewServerEnvironment = "Test";
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

/// <summary>One server row: its identity/network details, plus the inline "add credential" form.</summary>
public sealed partial class ServerRowViewModel : ObservableObject
{
	private readonly Guid _serverId;
	private readonly IIrisApiClient _api;

	public ServerRowViewModel(ServerResponse server, IIrisApiClient api)
	{
		_serverId = server.Id;
		_api = api;
		Name = server.Name;
		Hostname = server.Hostname;
		Os = server.Os;
		HostingType = server.HostingType;
		PublicIpAddress = server.PublicIpAddress;
		PrivateIpAddress = server.PrivateIpAddress;
		Environment = server.Environment;
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

	public IReadOnlyList<string> AuthMethods { get; } = ["Password", "SshKey"];

	[ObservableProperty] private bool _isAddCredentialPanelOpen;
	[ObservableProperty] private string _newUsername = string.Empty;
	[ObservableProperty] private string _newAuthMethod = "Password";
	[ObservableProperty] private string _newSecretValue = string.Empty;
	[ObservableProperty] private string _newLabel = string.Empty;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _credentialError;

	public bool HasCredentialError => !string.IsNullOrEmpty(CredentialError);

	partial void OnCredentialErrorChanged(string? value) => OnPropertyChanged(nameof(HasCredentialError));

	[RelayCommand]
	private void ToggleAddCredentialPanel() => IsAddCredentialPanelOpen = !IsAddCredentialPanelOpen;

	[RelayCommand]
	private async Task AddCredentialAsync()
	{
		var username = NewUsername.Trim();
		if (username.Length == 0)
		{
			CredentialError = "Username is required.";
			return;
		}

		if (string.IsNullOrEmpty(NewSecretValue))
		{
			CredentialError = "Enter a password or SSH key.";
			return;
		}

		IsBusy = true;
		CredentialError = null;

		try
		{
			var label = string.IsNullOrWhiteSpace(NewLabel) ? null : NewLabel.Trim();
			var request = new AddServerCredentialRequest(username, NewAuthMethod, NewSecretValue, label);
			var result = await _api.AddServerCredentialAsync(_serverId, request);
			Credentials.Add(new CredentialRowViewModel(result, this));

			IsAddCredentialPanelOpen = false;
			NewUsername = string.Empty;
			NewAuthMethod = "Password";
			NewSecretValue = string.Empty;
			NewLabel = string.Empty;
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
/// One OS-login credential. A thin UI wrapper around <see cref="ServerCredentialResponse"/>
/// carrying a back-reference to its owning <see cref="ServerRowViewModel"/> so the "Revoke"
/// button in the nested credentials list can bind to <c>{Binding Owner.RemoveCredentialCommand}</c>.
/// Never carries a secret value — the API never returns one.
/// </summary>
public sealed class CredentialRowViewModel(ServerCredentialResponse credential, ServerRowViewModel owner)
{
	public Guid Id => credential.Id;

	public string Username => credential.Username;

	public string AuthMethod => credential.AuthMethod;

	public string? Label => credential.Label;

	public ServerRowViewModel Owner => owner;
}
