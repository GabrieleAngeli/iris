using Iris.App.ViewModels;

namespace Iris.App.Views;

public partial class FirstLoginPasswordPage : ContentPage
{
	public FirstLoginPasswordPage(FirstLoginPasswordViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
