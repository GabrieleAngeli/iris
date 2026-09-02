using Iris.Contracts.Access;

namespace Iris.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
	private readonly IAuthService _auth;
	private readonly IIrisApiClient _api;

	public LoginViewModel(IAuthService auth, IIrisApiClient api)
	{
		_auth = auth;
		_api = api;
	}

	[ObservableProperty] private string _username = string.Empty;
	[ObservableProperty] private string _password = string.Empty;
	[ObservableProperty] private bool _rememberMe;
	[ObservableProperty] private bool _isPasswordHidden = true;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _errorMessage;

	public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

	partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnIsBusyChanged(bool value)
	{
		SignInCommand.NotifyCanExecuteChanged();
		ForgotPasswordCommand.NotifyCanExecuteChanged();
		UseSsoCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand]
	private void TogglePasswordVisibility() => IsPasswordHidden = !IsPasswordHidden;

	[RelayCommand(CanExecute = nameof(CanSignIn))]
	private async Task SignInAsync()
	{
		if (IsBusy)
			return;

		IsBusy = true;
		ErrorMessage = null;

		try
		{
			var result = await _auth.SignInAsync(Username, Password, RememberMe);
			if (!result.Success)
			{
				ErrorMessage = result.Error;
				return;
			}

			Password = string.Empty;

			// A pre-provisioned user signing in without SSO is asked to set a local password (or skip).
			var next = _auth.Me?.PasswordSetupPending == true ? "//first-login" : "//dashboard";
			await Shell.Current.GoToAsync(next);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool CanSignIn() => !IsBusy;

	[RelayCommand(CanExecute = nameof(CanSignIn))]
	private async Task ForgotPasswordAsync()
	{
		var email = Username.Trim();
		if (email.Length == 0)
		{
			ErrorMessage = "Enter your email first.";
			return;
		}

		IsBusy = true;
		ErrorMessage = null;

		try
		{
			await _api.RequestPasswordResetAsync(new RequestPasswordResetRequest(email));
			await Shell.Current.DisplayAlert("Password recovery",
				"If the account exists, Iris has sent a password reset invitation.",
				"OK");
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

	public async Task<bool> TryResumeAsync()
	{
		var result = await _auth.TryResumeRememberedSessionAsync();
		if (!result.Success)
		{
			return false;
		}

		await Shell.Current.GoToAsync("//dashboard");
		return true;
	}

	/// <summary>Raised when the operator has an invitation link/token to redeem.</summary>
	public event EventHandler? AcceptInvitationRequested;

	[RelayCommand]
	private void RequestAcceptInvitation() => AcceptInvitationRequested?.Invoke(this, EventArgs.Empty);

	[RelayCommand(CanExecute = nameof(CanSignIn))]
	private async Task UseSsoAsync()
	{
		if (IsBusy)
			return;

		IsBusy = true;
		ErrorMessage = null;

		try
		{
			var result = await _auth.SignInWithSsoAsync();
			if (!result.Success)
			{
				ErrorMessage = result.Error;
				return;
			}

			await Shell.Current.GoToAsync("//dashboard");
		}
		finally
		{
			IsBusy = false;
		}
	}
}
