namespace Iris.App.ViewModels;

/// <summary>
/// Backs the flyout's account card and governs which governance-only flyout items are
/// visible. Lives for the app's lifetime (registered as a singleton alongside <c>AppShell</c>)
/// and refreshes whenever <see cref="IAuthService.StateChanged"/> fires.
/// </summary>
public sealed partial class AppShellViewModel : ObservableObject, IDisposable
{
	private readonly IAuthService _auth;

	public AppShellViewModel(IAuthService auth)
	{
		_auth = auth;
		_auth.StateChanged += OnAuthStateChanged;
		SetCurrentRoute("startup");
	}

	public string DisplayName => _auth.Me?.DisplayName is { Length: > 0 } name ? name : "Signed out";

	public string Email => _auth.Me?.Email ?? string.Empty;

	public string Initials => ComputeInitials(DisplayName);

	/// <summary>
	/// True when the caller's Global-scope permissions include <c>governance.read</c>.
	/// <c>/governance/users</c> has no customerId/contextId route parameter, so
	/// <c>PermissionAuthorizationHandler</c> always evaluates it at Global scope — exactly
	/// what the unscoped <c>/me</c> call already returned into <see cref="IAuthService.Me"/>.
	/// </summary>
	public bool CanManageUsers => _auth.Me?.EffectivePermissions.Contains("governance.read") == true;

	/// <summary>
	/// True when the caller's Global-scope permissions include <c>infrastructure.read</c>.
	/// Same reasoning as <see cref="CanManageUsers"/>: <c>/servers</c> has no scope route
	/// parameter, so it's always checked at Global scope.
	/// </summary>
	public bool CanManageInfrastructure => _auth.Me?.EffectivePermissions.Contains("infrastructure.read") == true;

	/// <summary>The application inventory is a workspace surface, gated by Global <c>applications.read</c>.</summary>
	public bool CanSeeApplications => _auth.Me?.EffectivePermissions.Contains("applications.read") == true;

	[ObservableProperty] private string _currentRoute = "startup";
	[ObservableProperty] private bool _isWorkspaceExpanded;
	[ObservableProperty] private bool _isGovernanceExpanded;
	[ObservableProperty] private bool _isInfrastructureExpanded;
	[ObservableProperty] private bool _isApplicationsExpanded;
	[ObservableProperty] private bool _isDevelopmentExpanded;

	public bool IsDashboardActive => CurrentRoute == "dashboard";

	public bool IsAccessActive => CurrentRoute == "access";

	public bool IsUsersActive => CurrentRoute == "users";

	public bool IsCustomersActive => CurrentRoute == "customers";

	public bool IsServersActive => CurrentRoute == "servers";

	public bool IsApplicationsInventoryActive => CurrentRoute == "applications";

	public bool IsExtractorGuideActive => CurrentRoute == "extractor-guide";

	public bool IsComponentsActive => CurrentRoute == "components";

	public bool IsProfileActive => CurrentRoute == "profile";

	public bool IsSystemSettingsActive => CurrentRoute == "system-settings";

	public bool IsWorkspaceActive => IsAccessActive;

	public bool IsGovernanceActive => IsUsersActive || IsCustomersActive;

	public bool IsInfrastructureActive => IsServersActive;

	public bool IsApplicationsActive => IsApplicationsInventoryActive || IsExtractorGuideActive;

	public bool IsDevelopmentActive => IsComponentsActive;

	public string WorkspaceChevron => IsWorkspaceExpanded ? "\uE70D" : "\uE76C";

	public string GovernanceChevron => IsGovernanceExpanded ? "\uE70D" : "\uE76C";

	public string InfrastructureChevron => IsInfrastructureExpanded ? "\uE70D" : "\uE76C";

	public string ApplicationsChevron => IsApplicationsExpanded ? "\uE70D" : "\uE76C";

	public string DevelopmentChevron => IsDevelopmentExpanded ? "\uE70D" : "\uE76C";

	public bool IsDevelopment
	{
		get
		{
#if DEBUG
			return true;
#else
			return false;
#endif
		}
	}

	/// <summary>Navigates to an absolute Shell route (e.g. <c>//dashboard</c>) and closes the flyout.</summary>
	[RelayCommand]
	private async Task Navigate(string? route)
	{
		if (string.IsNullOrWhiteSpace(route) || Shell.Current is not { } shell)
		{
			return;
		}

		SetCurrentRoute(route);
		shell.FlyoutIsPresented = false;
		await shell.GoToAsync(route);
	}

	[RelayCommand]
	private void ToggleSection(string? section)
	{
		switch (section)
		{
			case "Workspace":
				IsWorkspaceExpanded = IsWorkspaceActive || !IsWorkspaceExpanded;
				break;
			case "Governance":
				IsGovernanceExpanded = IsGovernanceActive || !IsGovernanceExpanded;
				break;
			case "Infrastructure":
				IsInfrastructureExpanded = IsInfrastructureActive || !IsInfrastructureExpanded;
				break;
			case "Applications":
				IsApplicationsExpanded = IsApplicationsActive || !IsApplicationsExpanded;
				break;
			case "Development":
				IsDevelopmentExpanded = IsDevelopmentActive || !IsDevelopmentExpanded;
				break;
		}
	}

	public void SetCurrentRoute(string? route)
	{
		var normalized = NormalizeRoute(route);
		if (CurrentRoute != normalized)
		{
			CurrentRoute = normalized;
		}

		EnsureActiveSectionExpanded();
	}

	private void OnAuthStateChanged(object? sender, EventArgs e)
	{
		OnPropertyChanged(nameof(DisplayName));
		OnPropertyChanged(nameof(Email));
		OnPropertyChanged(nameof(Initials));
		OnPropertyChanged(nameof(CanManageUsers));
		OnPropertyChanged(nameof(CanManageInfrastructure));
		OnPropertyChanged(nameof(CanSeeApplications));
	}

	partial void OnCurrentRouteChanged(string value)
	{
		OnPropertyChanged(nameof(IsDashboardActive));
		OnPropertyChanged(nameof(IsAccessActive));
		OnPropertyChanged(nameof(IsUsersActive));
		OnPropertyChanged(nameof(IsCustomersActive));
		OnPropertyChanged(nameof(IsServersActive));
		OnPropertyChanged(nameof(IsApplicationsInventoryActive));
		OnPropertyChanged(nameof(IsExtractorGuideActive));
		OnPropertyChanged(nameof(IsComponentsActive));
		OnPropertyChanged(nameof(IsProfileActive));
		OnPropertyChanged(nameof(IsSystemSettingsActive));
		OnPropertyChanged(nameof(IsWorkspaceActive));
		OnPropertyChanged(nameof(IsGovernanceActive));
		OnPropertyChanged(nameof(IsInfrastructureActive));
		OnPropertyChanged(nameof(IsApplicationsActive));
		OnPropertyChanged(nameof(IsDevelopmentActive));
	}

	partial void OnIsWorkspaceExpandedChanged(bool value) => OnPropertyChanged(nameof(WorkspaceChevron));

	partial void OnIsGovernanceExpandedChanged(bool value) => OnPropertyChanged(nameof(GovernanceChevron));

	partial void OnIsInfrastructureExpandedChanged(bool value) => OnPropertyChanged(nameof(InfrastructureChevron));

	partial void OnIsApplicationsExpandedChanged(bool value) => OnPropertyChanged(nameof(ApplicationsChevron));

	partial void OnIsDevelopmentExpandedChanged(bool value) => OnPropertyChanged(nameof(DevelopmentChevron));

	private void EnsureActiveSectionExpanded()
	{
		if (IsWorkspaceActive)
		{
			IsWorkspaceExpanded = true;
		}

		if (IsGovernanceActive)
		{
			IsGovernanceExpanded = true;
		}

		if (IsInfrastructureActive)
		{
			IsInfrastructureExpanded = true;
		}

		if (IsApplicationsActive)
		{
			IsApplicationsExpanded = true;
		}

		if (IsDevelopmentActive)
		{
			IsDevelopmentExpanded = true;
		}
	}

	private static string NormalizeRoute(string? route)
	{
		if (string.IsNullOrWhiteSpace(route))
		{
			return string.Empty;
		}

		var path = route.Split('?', 2)[0].Trim('/');
		if (path.Length == 0)
		{
			return string.Empty;
		}

		var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
		return parts.Length == 0 ? path : parts[^1];
	}

	private static string ComputeInitials(string displayName)
	{
		var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		return parts switch
		{
			[] => "?",
			[var only] => only[..Math.Min(2, only.Length)].ToUpperInvariant(),
			_ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant(),
		};
	}

	public void Dispose() => _auth.StateChanged -= OnAuthStateChanged;
}
