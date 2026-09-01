using Iris.App.Services;
using Iris.Contracts.Access;

namespace Iris.App.ViewModels;

/// <summary>
/// Shown once, right after a pre-provisioned user signs in without SSO: they can set a local
/// password (used for future non-SSO sign-ins) or skip. Either way they land on the dashboard.
/// </summary>
public partial class FirstLoginPasswordViewModel : ObservableObject
{
	private const int MinimumLength = 8;

	private readonly IIrisApiClient _api;
	private readonly IAuthService _auth;

	public FirstLoginPasswordViewModel(IIrisApiClient api, IAuthService auth)
	{
		_api = api;
		_auth = auth;
	}

	[ObservableProperty] private string _newPassword = string.Empty;
	[ObservableProperty] private string _confirmPassword = string.Empty;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _errorMessage;

	public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

	partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnIsBusyChanged(bool value)
	{
		SetPasswordCommand.NotifyCanExecuteChanged();
		SkipCommand.NotifyCanExecuteChanged();
	}

	private bool NotBusy => !IsBusy;

	[RelayCommand(CanExecute = nameof(NotBusy))]
	private async Task SetPasswordAsync()
	{
		var password = NewPassword;
		if (password.Length < MinimumLength)
		{
			ErrorMessage = $"Use at least {MinimumLength} characters.";
			return;
		}

		if (!string.Equals(password, ConfirmPassword, StringComparison.Ordinal))
		{
			ErrorMessage = "The two passwords don't match.";
			return;
		}

		IsBusy = true;
		ErrorMessage = null;

		try
		{
			await _api.SetPasswordAsync(new SetPasswordRequest(password));
			// The API now expects this password on every dev-mode call.
			_auth.UseLocalPassword(password);
			await GoToDashboardAsync();
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			ErrorMessage = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand(CanExecute = nameof(NotBusy))]
	private async Task SkipAsync()
	{
		IsBusy = true;
		ErrorMessage = null;

		try
		{
			await _api.SkipPasswordSetupAsync();
			await GoToDashboardAsync();
		}
		catch (Exception ex) when (ex is IrisApiException or HttpRequestException)
		{
			ErrorMessage = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task GoToDashboardAsync()
	{
		NewPassword = string.Empty;
		ConfirmPassword = string.Empty;
		await Shell.Current.GoToAsync("//dashboard");
	}
}
