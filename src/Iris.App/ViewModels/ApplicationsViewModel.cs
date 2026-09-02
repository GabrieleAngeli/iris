using System.Collections.ObjectModel;
using Iris.Contracts.Applications;

namespace Iris.App.ViewModels;

/// <summary>Workspace > Applications: catalog inventory with create + guarded edit.</summary>
public partial class ApplicationsViewModel : ObservableObject
{
	private const string ReadPermission = "applications.read";
	private const string WritePermission = "applications.write";

	private readonly IIrisApiClient _api;
	private readonly IAuthService _auth;

	public ApplicationsViewModel(IIrisApiClient api, IAuthService auth)
	{
		_api = api;
		_auth = auth;
	}

	public ObservableCollection<ApplicationRowViewModel> Applications { get; } = [];

	public IReadOnlyList<string> RuntimeTypes { get; } = ["CSharp", "JavaScript", "Java", "Node", "Docker"];

	[ObservableProperty] private bool _isLoading;
	[ObservableProperty] private string? _error;

	public bool HasError => !string.IsNullOrEmpty(Error);

	public bool CanSeeApplications => _auth.Me?.EffectivePermissions.Contains(ReadPermission) == true;

	public bool CanManageApplications => _auth.Me?.EffectivePermissions.Contains(WritePermission) == true;

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
			var applications = await _api.GetApplicationsAsync();
			Applications.Clear();
			foreach (var application in applications)
			{
				Applications.Add(new ApplicationRowViewModel(application, _api, this));
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

	public event EventHandler? NewApplicationRequested;

	public event EventHandler? NewApplicationCompleted;

	public event EventHandler<ApplicationRowViewModel>? EditApplicationRequested;

	public void RaiseEditRequested(ApplicationRowViewModel row) => EditApplicationRequested?.Invoke(this, row);

	[ObservableProperty] private string _newApplicationName = string.Empty;
	[ObservableProperty] private string _newApplicationSlug = string.Empty;
	[ObservableProperty] private string _newApplicationRuntimeType = "CSharp";
	[ObservableProperty] private string _newApplicationRepositoryUrl = string.Empty;
	[ObservableProperty] private string _newApplicationDefaultBranch = "main";
	[ObservableProperty] private string _newApplicationDescription = string.Empty;
	[ObservableProperty] private string _newApplicationArtifactProvider = string.Empty;
	[ObservableProperty] private string _newApplicationArtifactFeed = string.Empty;
	[ObservableProperty] private string _newApplicationArtifactName = string.Empty;
	[ObservableProperty] private string _newApplicationArtifactPath = string.Empty;
	[ObservableProperty] private string _newApplicationBuildPipelineUrl = string.Empty;
	[ObservableProperty] private bool _isCreatingApplication;
	[ObservableProperty] private string? _createApplicationError;

	public bool HasCreateApplicationError => !string.IsNullOrEmpty(CreateApplicationError);

	partial void OnCreateApplicationErrorChanged(string? value) => OnPropertyChanged(nameof(HasCreateApplicationError));

	[RelayCommand]
	private void RequestNewApplication()
	{
		NewApplicationName = string.Empty;
		NewApplicationSlug = string.Empty;
		NewApplicationRuntimeType = RuntimeTypes[0];
		NewApplicationRepositoryUrl = string.Empty;
		NewApplicationDefaultBranch = "main";
		NewApplicationDescription = string.Empty;
		NewApplicationArtifactProvider = string.Empty;
		NewApplicationArtifactFeed = string.Empty;
		NewApplicationArtifactName = string.Empty;
		NewApplicationArtifactPath = string.Empty;
		NewApplicationBuildPipelineUrl = string.Empty;
		CreateApplicationError = null;
		NewApplicationRequested?.Invoke(this, EventArgs.Empty);
	}

	[RelayCommand]
	private async Task CreateApplicationAsync()
	{
		var name = NewApplicationName.Trim();
		var repositoryUrl = NewApplicationRepositoryUrl.Trim();
		var defaultBranch = NewApplicationDefaultBranch.Trim();

		if (name.Length == 0 || repositoryUrl.Length == 0 || defaultBranch.Length == 0)
		{
			CreateApplicationError = "Name, repository URL and default branch are required.";
			return;
		}

		IsCreatingApplication = true;
		CreateApplicationError = null;

		try
		{
			var created = await _api.CreateApplicationAsync(new CreateApplicationRequest(
				name,
				string.IsNullOrWhiteSpace(NewApplicationSlug) ? null : NewApplicationSlug.Trim(),
				NewApplicationRuntimeType,
				repositoryUrl,
				defaultBranch,
				NewApplicationDescription,
				Clean(NewApplicationArtifactProvider),
				Clean(NewApplicationArtifactFeed),
				Clean(NewApplicationArtifactName),
				Clean(NewApplicationArtifactPath),
				Clean(NewApplicationBuildPipelineUrl)));

			Applications.Insert(0, new ApplicationRowViewModel(created, _api, this));
			NewApplicationCompleted?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			CreateApplicationError = ex.Message;
		}
		finally
		{
			IsCreatingApplication = false;
		}
	}

	internal static string? Clean(string value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed partial class ApplicationRowViewModel : ObservableObject
{
	private const string LockResource = "application";
	private const int HeartbeatSeconds = 45;

	private readonly Guid _applicationId;
	private readonly IIrisApiClient _api;
	private readonly ApplicationsViewModel _parent;
	private CancellationTokenSource? _heartbeatCts;

	public ApplicationRowViewModel(ApplicationResponse application, IIrisApiClient api, ApplicationsViewModel parent)
	{
		_applicationId = application.Id;
		_api = api;
		_parent = parent;
		ApplyFrom(application);
	}

	public Guid Id => _applicationId;

	public IReadOnlyList<string> RuntimeTypes => _parent.RuntimeTypes;

	public bool CanManageApplications => _parent.CanManageApplications;

	[ObservableProperty] private string _name = string.Empty;
	[ObservableProperty] private string _slug = string.Empty;
	[ObservableProperty] private string _runtimeType = string.Empty;
	[ObservableProperty] private string _repositoryUrl = string.Empty;
	[ObservableProperty] private string _defaultBranch = string.Empty;
	[ObservableProperty] private string? _description;
	[ObservableProperty] private string? _artifactProvider;
	[ObservableProperty] private string? _artifactFeed;
	[ObservableProperty] private string? _artifactName;
	[ObservableProperty] private string? _artifactPath;
	[ObservableProperty] private string? _buildPipelineUrl;
	[ObservableProperty] private bool _isActive;
	[ObservableProperty] private int _versionCount;
	[ObservableProperty] private int _configurationKeyCount;
	[ObservableProperty] private int _dependencyCount;
	[ObservableProperty] private int _placeholderCount;
	[ObservableProperty] private DateTimeOffset? _lastImportedAtUtc;

	public string VersionCountText => VersionCount == 1 ? "1 version" : $"{VersionCount} versions";

	public string KnowledgeSummary => $"{ConfigurationKeyCount} keys | {DependencyCount} dependencies | {PlaceholderCount} placeholders";

	public string LastImportText => LastImportedAtUtc is { } value
		? $"Last import: {value.ToLocalTime():g}"
		: "No imported knowledge yet";

	public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

	public bool HasArtifact => !string.IsNullOrWhiteSpace(ArtifactProvider) ||
		!string.IsNullOrWhiteSpace(ArtifactFeed) ||
		!string.IsNullOrWhiteSpace(ArtifactName) ||
		!string.IsNullOrWhiteSpace(ArtifactPath) ||
		!string.IsNullOrWhiteSpace(BuildPipelineUrl);

	public string ArtifactSummary
	{
		get
		{
			var provider = string.IsNullOrWhiteSpace(ArtifactProvider) ? "artifact" : ArtifactProvider;
			var name = string.IsNullOrWhiteSpace(ArtifactName) ? ArtifactPath : ArtifactName;
			return string.IsNullOrWhiteSpace(name)
				? provider
				: $"{provider}: {name}";
		}
	}

	partial void OnDescriptionChanged(string? value) => OnPropertyChanged(nameof(HasDescription));

	private void ApplyFrom(ApplicationResponse application)
	{
		Name = application.Name;
		Slug = application.Slug;
		RuntimeType = application.RuntimeType;
		RepositoryUrl = application.RepositoryUrl;
		DefaultBranch = application.DefaultBranch;
		Description = application.Description;
		ArtifactProvider = application.ArtifactProvider;
		ArtifactFeed = application.ArtifactFeed;
		ArtifactName = application.ArtifactName;
		ArtifactPath = application.ArtifactPath;
		BuildPipelineUrl = application.BuildPipelineUrl;
		IsActive = application.IsActive;
		VersionCount = application.Versions.Count;
		ConfigurationKeyCount = application.Versions.Sum(v => v.ConfigurationKeyCount);
		DependencyCount = application.Versions.Sum(v => v.DependencyCount);
		PlaceholderCount = application.Versions.Sum(v => v.PlaceholderCount);
		LastImportedAtUtc = application.Versions
			.Select(v => v.LastImportedAtUtc)
			.Max();

		OnPropertyChanged(nameof(VersionCountText));
		OnPropertyChanged(nameof(KnowledgeSummary));
		OnPropertyChanged(nameof(LastImportText));
		OnPropertyChanged(nameof(HasArtifact));
		OnPropertyChanged(nameof(ArtifactSummary));
	}

	[ObservableProperty] private string _editName = string.Empty;
	[ObservableProperty] private string _editRuntimeType = "CSharp";
	[ObservableProperty] private string _editRepositoryUrl = string.Empty;
	[ObservableProperty] private string _editDefaultBranch = "main";
	[ObservableProperty] private string _editDescription = string.Empty;
	[ObservableProperty] private string _editArtifactProvider = string.Empty;
	[ObservableProperty] private string _editArtifactFeed = string.Empty;
	[ObservableProperty] private string _editArtifactName = string.Empty;
	[ObservableProperty] private string _editArtifactPath = string.Empty;
	[ObservableProperty] private string _editBuildPipelineUrl = string.Empty;
	[ObservableProperty] private bool _editActive;
	[ObservableProperty] private bool _isEditBusy;
	[ObservableProperty] private string? _editError;
	[ObservableProperty] private string? _editLockNotice;

	public bool HasEditError => !string.IsNullOrEmpty(EditError);

	public bool HasEditLockNotice => !string.IsNullOrEmpty(EditLockNotice);

	partial void OnEditErrorChanged(string? value) => OnPropertyChanged(nameof(HasEditError));

	partial void OnEditLockNoticeChanged(string? value) => OnPropertyChanged(nameof(HasEditLockNotice));

	[RelayCommand]
	private async Task OpenEditAsync()
	{
		EditLockNotice = null;
		EditError = null;

		try
		{
			var slot = await _api.AcquireEditLockAsync(LockResource, _applicationId);
			if (!slot.Mine)
			{
				EditLockNotice = $"{slot.HolderDisplayName} is editing this application right now - try again in a moment.";
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
		EditRuntimeType = RuntimeType;
		EditRepositoryUrl = RepositoryUrl;
		EditDefaultBranch = DefaultBranch;
		EditDescription = Description ?? string.Empty;
		EditArtifactProvider = ArtifactProvider ?? string.Empty;
		EditArtifactFeed = ArtifactFeed ?? string.Empty;
		EditArtifactName = ArtifactName ?? string.Empty;
		EditArtifactPath = ArtifactPath ?? string.Empty;
		EditBuildPipelineUrl = BuildPipelineUrl ?? string.Empty;
		EditActive = IsActive;
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
					await _api.AcquireEditLockAsync(LockResource, _applicationId, token).ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
				{
					// A dropped heartbeat just lets the lock lapse sooner.
				}
			}
		}
		catch (OperationCanceledException)
		{
			// editor closed
		}
	}

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
			await _api.ReleaseEditLockAsync(LockResource, _applicationId).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			// The lock will expire on its own if the release didn't land.
		}
	}

	public event EventHandler? EditCompleted;

	[RelayCommand]
	private async Task SaveEditAsync()
	{
		var name = EditName.Trim();
		var repositoryUrl = EditRepositoryUrl.Trim();
		var defaultBranch = EditDefaultBranch.Trim();

		if (name.Length == 0 || repositoryUrl.Length == 0 || defaultBranch.Length == 0)
		{
			EditError = "Name, repository URL and default branch are required.";
			return;
		}

		IsEditBusy = true;
		EditError = null;

		try
		{
			var updated = await _api.UpdateApplicationAsync(_applicationId, new UpdateApplicationRequest(
				name,
				EditRuntimeType,
				repositoryUrl,
				defaultBranch,
				EditDescription,
				EditActive,
				ApplicationsViewModel.Clean(EditArtifactProvider),
				ApplicationsViewModel.Clean(EditArtifactFeed),
				ApplicationsViewModel.Clean(EditArtifactName),
				ApplicationsViewModel.Clean(EditArtifactPath),
				ApplicationsViewModel.Clean(EditBuildPipelineUrl)));

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
}
