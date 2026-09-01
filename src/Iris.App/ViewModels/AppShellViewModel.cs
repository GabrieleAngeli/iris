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

	/// <summary>Navigates to an absolute Shell route (e.g. <c>//dashboard</c>) and closes the flyout.</summary>
	[RelayCommand]
	private async Task Navigate(string? route)
	{
		if (string.IsNullOrWhiteSpace(route) || Shell.Current is not { } shell)
		{
			return;
		}

		shell.FlyoutIsPresented = false;
		await shell.GoToAsync(route);
	}

	private void OnAuthStateChanged(object? sender, EventArgs e)
	{
		OnPropertyChanged(nameof(DisplayName));
		OnPropertyChanged(nameof(Email));
		OnPropertyChanged(nameof(Initials));
		OnPropertyChanged(nameof(CanManageUsers));
		OnPropertyChanged(nameof(CanManageInfrastructure));
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
