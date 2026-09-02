namespace Iris.App.Views;

public partial class LoginPage : ContentPage
{
	public LoginPage(LoginViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;

		vm.AcceptInvitationRequested += async (_, _) => await Shell.Current.GoToAsync("//accept-invitation");
	}
}
