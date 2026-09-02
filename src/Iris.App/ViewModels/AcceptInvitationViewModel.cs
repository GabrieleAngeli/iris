using Iris.Contracts.Governance;

namespace Iris.App.ViewModels;

/// <summary>
/// Redeems a one-time invitation link: the recipient pastes the link (or the bare token) an
/// administrator sent them and sets their first local password — for anyone without an SSO
/// platform to lean on. On success they return to the normal sign-in screen and use that
/// password there, which reconciles their identity the same way a first sign-in already does.
/// </summary>
public partial class AcceptInvitationViewModel : ObservableObject
{
	private const int MinimumLength = 8;

	private readonly IIrisApiClient _api;

	public AcceptInvitationViewModel(IIrisApiClient api)
	{
		_api = api;
	}

	[ObservableProperty] private string _tokenOrLink = string.Empty;
	[ObservableProperty] private string _newPassword = string.Empty;
	[ObservableProperty] private string _confirmPassword = string.Empty;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private string? _errorMessage;

	public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

	partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

	/// <summary>Raised once the password is set — the page shows the confirmation and returns to sign-in.</summary>
	public event EventHandler<string>? Accepted;

	[RelayCommand(CanExecute = nameof(NotBusy))]
	private async Task AcceptAsync()
	{
		var token = ExtractToken(TokenOrLink);
		if (token.Length == 0)
		{
			ErrorMessage = "Paste the invitation link or token an administrator sent you.";
			return;
		}

		if (NewPassword.Length < MinimumLength)
		{
			ErrorMessage = $"Use at least {MinimumLength} characters.";
			return;
		}

		if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
		{
			ErrorMessage = "The two passwords don't match.";
			return;
		}

		IsBusy = true;
		ErrorMessage = null;

		try
		{
			var result = await _api.AcceptInvitationAsync(new AcceptInvitationRequest(token, NewPassword));
			TokenOrLink = string.Empty;
			NewPassword = string.Empty;
			ConfirmPassword = string.Empty;
			Accepted?.Invoke(this, result.Email);
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

	private bool NotBusy => !IsBusy;

	partial void OnIsBusyChanged(bool value) => AcceptCommand.NotifyCanExecuteChanged();

	/// <summary>Accepts either a bare token or a full accept link — pulls the <c>token</c> query value out of the latter.</summary>
	private static string ExtractToken(string input)
	{
		var trimmed = input.Trim();
		if (trimmed.Length == 0)
		{
			return trimmed;
		}

		var markerIndex = trimmed.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
		if (markerIndex < 0)
		{
			return trimmed;
		}

		var start = markerIndex + "token=".Length;
		var end = trimmed.IndexOf('&', start);
		var raw = end < 0 ? trimmed[start..] : trimmed[start..end];
		return Uri.UnescapeDataString(raw);
	}
}
