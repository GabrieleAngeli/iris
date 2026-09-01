namespace Iris.App.Views;

public partial class AcceptInvitationPage : ContentPage
{
	private readonly AcceptInvitationViewModel _vm;

	public AcceptInvitationPage(AcceptInvitationViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;

		_vm.Accepted += OnAccepted;
	}

	private async void OnAccepted(object? sender, string email)
	{
		await Shell.Current.DisplayAlert(
			"Password set", $"You can now sign in as {email} with the password you just set.", "Sign in");
		await Shell.Current.GoToAsync("//login");
	}

	private async void OnBackToSignIn(object? sender, EventArgs e) =>
		await Shell.Current.GoToAsync("//login");
}
