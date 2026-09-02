using System.Collections.ObjectModel;
using Iris.Contracts.Access;

namespace Iris.App.ViewModels;

public partial class ProfileViewModel(IIrisApiClient api) : ObservableObject
{
	[ObservableProperty] private string _displayName = string.Empty;
	[ObservableProperty] private string _email = string.Empty;
	[ObservableProperty] private string _scope = string.Empty;
	[ObservableProperty] private string _currentPassword = string.Empty;
	[ObservableProperty] private string _newPassword = string.Empty;
	[ObservableProperty] private string _confirmPassword = string.Empty;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _error;
	[ObservableProperty] private string? _status;

	public ObservableCollection<string> Permissions { get; } = [];

	public ObservableCollection<AccessHistoryRow> AccessHistory { get; } = [];

	public bool HasError => !string.IsNullOrEmpty(Error);

	public bool HasStatus => !string.IsNullOrEmpty(Status);

	partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnStatusChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

	partial void OnIsBusyChanged(bool value)
	{
		LoadCommand.NotifyCanExecuteChanged();
		ChangePasswordCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand(CanExecute = nameof(CanRunCommand))]
	private async Task LoadAsync()
	{
		IsBusy = true;
		Error = null;

		try
		{
			var profile = await api.GetProfileAsync();
			DisplayName = profile.Me.DisplayName;
			Email = profile.Me.Email;
			Scope = profile.Me.EvaluatedScope;

			Permissions.Clear();
			foreach (var permission in profile.Me.EffectivePermissions.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
			{
				Permissions.Add(permission);
			}

			AccessHistory.Clear();
			foreach (var access in profile.AccessHistory)
			{
				AccessHistory.Add(new AccessHistoryRow(access));
			}
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

	[RelayCommand(CanExecute = nameof(CanRunCommand))]
	private async Task ChangePasswordAsync()
	{
		if (NewPassword.Length < 8)
		{
			Error = "Use at least 8 characters.";
			return;
		}

		if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
		{
			Error = "The two passwords do not match.";
			return;
		}

		IsBusy = true;
		Error = null;
		Status = null;

		try
		{
			await api.SetPasswordAsync(new SetPasswordRequest(
				NewPassword,
				string.IsNullOrEmpty(CurrentPassword) ? null : CurrentPassword));

			CurrentPassword = string.Empty;
			NewPassword = string.Empty;
			ConfirmPassword = string.Empty;
			Status = "Password updated.";
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

	private bool CanRunCommand() => !IsBusy;
}

public sealed class AccessHistoryRow(AccessHistoryResponse response)
{
	public string Method => response.Method;

	public string SignedInAt => response.SignedInAtUtc.ToLocalTime().ToString("g");

	public string ExpiresAt => response.ExpiresAtUtc.ToLocalTime().ToString("g");

	public string Current => response.IsCurrent ? "Current" : string.Empty;
}
