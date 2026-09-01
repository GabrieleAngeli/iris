namespace Iris.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
	private readonly IAuthService _auth;

	public LoginViewModel(IAuthService auth)
	{
		_auth = auth;
	}

	[ObservableProperty] private string _username = "admin@iris.local";
	[ObservableProperty] private string _password = string.Empty;
	[ObservableProperty] private bool _rememberMe = true;
	[ObservableProperty] private bool _isPasswordHidden = true;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _errorMessage;

	public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

	partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

	partial void OnIsBusyChanged(bool value)
	{
		SignInCommand.NotifyCanExecuteChanged();
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
			var result = await _auth.SignInAsync(Username, Password);
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

	[RelayCommand]
	private async Task ForgotPasswordAsync() =>
		await Shell.Current.DisplayAlert("Sign in",
			"Dev mode: the user name is a configured Iris dev user (e.g. admin@iris.local). The password is ignored.",
			"Got it");

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
